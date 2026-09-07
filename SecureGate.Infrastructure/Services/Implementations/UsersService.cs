using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain;
using SecureGate.Infrastructure.Hubs;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class UsersService : IUsersService
    {
        private readonly AppDbContext _db;
        private readonly IPhotoStorageService _photoStorage;
        private readonly IFaceRecognitionClient _faceClient;
        private readonly IKnownFaceCache _knownFaceCache;
        private readonly IHubContext<AlertHub> _alertHub;
        private readonly ILogger<UsersService> _logger;

        public UsersService(
            AppDbContext db,
            IPhotoStorageService photoStorage,
            IFaceRecognitionClient faceClient,
            IKnownFaceCache knownFaceCache,
            IHubContext<AlertHub> alertHub,
            ILogger<UsersService> logger)
        {
            _db = db;
            _photoStorage = photoStorage;
            _faceClient = faceClient;
            _knownFaceCache = knownFaceCache;
            _alertHub = alertHub;
            _logger = logger;
        }

        /// <summary>
        /// AlertHub → "NewAlert". Bloklash/blokdan chiqarish operatorlar ekranida
        /// darhol ko'rinishi uchun. SignalR xatosi bloklash amaliyotini
        /// HECH QACHON bekor qilmaydi — faqat LogWarning ga yoziladi.
        /// </summary>
        private async Task SendAlertAsync(string title, string message, string type)
        {
            try
            {
                await _alertHub.Clients.All.SendAsync("NewAlert", new
                {
                    title,
                    message,
                    type, // info | warning | danger | success
                    // ISO-8601 UTC — formatlash frontend ishi.
                    time = DateTime.UtcNow.ToString("O")
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NewAlert SignalR event yuborishda xato");
            }
        }

        // Bloklash/tahrirlash/o'chirishdan keyin xotiradagi yuz cache'ini darhol yangilaymiz —
        // aks holda bloklangan odam cache eskirgunicha (60s gacha) turniketdan o'tib ketardi.
        private async Task RefreshKnownFacesAsync()
        {
            try
            {
                await _knownFaceCache.ReloadAsync(force: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KnownFaceCache yangilanmadi (davriy yangilanish baribir ishlaydi)");
            }
        }

        public async Task<UsersListViewModel> GetStudentsAsync(string? search, StudentStatus? status, int page, int pageSize)
        {
            // Sahifalash chegaralari — mijoz pageSize=1000000 yuborib DB'ni cho'ktira olmasligi uchun.
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _db.Students.Include(s => s.BlockedUser).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => s.FirstName.Contains(search) || s.LastName.Contains(search) || (s.StudentId != null && s.StudentId.Contains(search)) || (s.Phone != null && s.Phone.Contains(search)));

            if (status.HasValue)
                query = query.Where(s => s.Status == status);

            var total = await query.CountAsync();
            var students = await query.OrderByDescending(s => s.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new UsersListViewModel
            {
                Students = students,
                SearchTerm = search,
                StatusFilter = status,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        public async Task<Users?> GetByIdAsync(int id) =>
            await _db.Students.Include(s => s.BlockedUser)
                .Include(s => s.TurnstilePermissions).ThenInclude(tp => tp.Turnstile)
                .FirstOrDefaultAsync(s => s.Id == id);

        public async Task<Users> CreateAsync(UsersCreateViewModel model)
        {
            // Rasm majburiy — yuzni tanish turniketda ishlashi uchun.
            // Fayl yozish TRANZAKSIYADAN TASHQARIDA — execution strategy qayta urinsa
            // delegate to'liq qaytadan ishlaydi va rasm ikki marta yozilib qolardi.
            var photoPath = await _photoStorage.SavePhotoAsync(model.PhotoFile, model.CapturedPhotoBase64, "users");
            if (string.IsNullOrEmpty(photoPath))
                throw new InvalidOperationException("Foydalanuvchi rasmi yuklanishi shart.");

            // Encoding — Python face-worker'ga HTTP chaqiruv. U ham tranzaksiyadan tashqarida
            // hisoblanadi (qayta urinishda takroriy tarmoq chaqiruvi bo'lmasligi uchun).
            // Python yo'q yoki yuz topilmagan bo'lsa, encoding null bo'lib qoladi
            // (admin keyinroq "Qayta hisoblash" tugmasi orqali sinab ko'rishi mumkin).
            var encoding = await _faceClient.ComputeEmbeddingAsync(photoPath);
            var encodingJson = encoding != null ? JsonSerializer.Serialize(encoding) : null;

            // Race-condition'siz StudentId hosil qilish (CameraService.CreateAsync bilan bir xil usul):
            //   1) Tranzaksiyada o'quvchini StudentId'siz saqlaymiz (IDENTITY Id'ni olamiz)
            //   2) Id asosida StudentId hosil qilib, ikkinchi SaveChanges'da yozamiz
            // "MAX(Id)+1" usuli parallel so'rovlarda bir xil raqam berardi.
            //
            // DIQQAT: UseSqlServer(EnableRetryOnFailure) yoqilganda foydalanuvchi boshlagan
            // tranzaksiya FAQAT execution strategy ichida ochilishi mumkin.
            Users student = null!;

            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();
                try
                {
                    student = new Users
                    {
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        DateOfBirth = model.DateOfBirth,
                        Gender = model.Gender,
                        Phone = model.Phone,
                        ParentPhone = model.ParentPhone,
                        Address = model.Address,
                        PhotoPath = photoPath,
                        FaceRecognitionEnabled = model.FaceRecognitionEnabled,
                        SmsNotification = model.SmsNotification,
                        StudentId = string.Empty, // vaqtinchalik, pastda to'ldiramiz
                        Status = StudentStatus.New
                    };

                    _db.Students.Add(student);
                    await _db.SaveChangesAsync(); // Id avtomatik to'ldiriladi

                    student.StudentId = student.Id.ToString("D6");
                    await _db.SaveChangesAsync();

                    _db.FaceData.Add(new FaceData
                    {
                        StudentId = student.Id,
                        ImagePath = photoPath,
                        FaceEncoding = encodingJson,
                        ConfidenceLevel = FaceConfidenceLevel.High,
                        IsActive = student.FaceRecognitionEnabled
                    });

                    // Turniket ruxsatlari
                    foreach (var turnstileId in model.AllowedTurnstileIds)
                    {
                        _db.TurnstilePermissions.Add(new TurnstilePermission
                        {
                            StudentId = student.Id,
                            TurnstileId = turnstileId,
                            IsAllowed = true
                        });
                    }
                    await _db.SaveChangesAsync();

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            if (encoding == null)
                _logger.LogWarning("Foydalanuvchi {Id} uchun encoding hisoblanmadi (Python yo'q yoki yuz topilmadi)", student.Id);

            await RefreshKnownFacesAsync();
            return student;
        }

        public async Task UpdateAsync(int id, UsersEditViewModel model)
        {
            var student = await _db.Students.FindAsync(id);
            if (student == null) return;

            student.FirstName = model.FirstName;
            student.LastName = model.LastName;
            student.DateOfBirth = model.DateOfBirth;
            student.Gender = model.Gender;
            student.Phone = model.Phone;
            student.ParentPhone = model.ParentPhone;
            student.Address = model.Address;
            student.FaceRecognitionEnabled = model.FaceRecognitionEnabled;
            student.SmsNotification = model.SmsNotification;
            student.UpdatedAt = DateTime.UtcNow;

            // Yangi rasm yuklangan bo'lsa - eskisini o'chirib, encodingni qayta hisoblash
            var newPhotoPath = await _photoStorage.SavePhotoAsync(model.PhotoFile, model.CapturedPhotoBase64, "users");
            if (!string.IsNullOrEmpty(newPhotoPath))
            {
                _photoStorage.DeletePhoto(student.PhotoPath);
                student.PhotoPath = newPhotoPath;

                var newEncoding = await _faceClient.ComputeEmbeddingAsync(newPhotoPath);
                var encodingJson = newEncoding != null ? JsonSerializer.Serialize(newEncoding) : null;

                var face = await _db.FaceData.FirstOrDefaultAsync(f => f.StudentId == id);
                if (face == null)
                {
                    _db.FaceData.Add(new FaceData
                    {
                        StudentId = id,
                        ImagePath = newPhotoPath,
                        FaceEncoding = encodingJson,
                        ConfidenceLevel = FaceConfidenceLevel.High,
                        IsActive = student.FaceRecognitionEnabled
                    });
                }
                else
                {
                    face.ImagePath = newPhotoPath;
                    face.FaceEncoding = encodingJson;
                    face.IsActive = student.FaceRecognitionEnabled;
                }
            }
            else
            {
                // FaceRecognitionEnabled holatini FaceData bilan sinxron tutamiz
                var face = await _db.FaceData.FirstOrDefaultAsync(f => f.StudentId == id);
                if (face != null) face.IsActive = student.FaceRecognitionEnabled;
            }

            await _db.SaveChangesAsync();
            await RefreshKnownFacesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var student = await _db.Students.FindAsync(id);
            if (student == null) return;

            var faces = await _db.FaceData.Where(f => f.StudentId == id).ToListAsync();
            _db.FaceData.RemoveRange(faces);

            var photoPath = student.PhotoPath;
            _db.Students.Remove(student);
            await _db.SaveChangesAsync();

            _photoStorage.DeletePhoto(photoPath);
            await RefreshKnownFacesAsync();
        }

        public async Task BlockAsync(int studentId, BlockUserViewModel model)
        {
            var student = await _db.Students.FindAsync(studentId);
            if (student == null) return;

            student.Status = StudentStatus.Blocked;

            _db.BlockedUsers.Add(new BlockedUser
            {
                StudentId = studentId,
                Reason = model.Reason,
                ReasonType = model.ReasonType,
                BlockedBy = model.BlockedBy,
                Duration = model.Duration
            });

            await _db.SaveChangesAsync();

            // Bloklangan odam turniketdan o'tib ketmasligi uchun cache DARHOL yangilanadi.
            await RefreshKnownFacesAsync();

            await SendAlertAsync(
                "Foydalanuvchi bloklandi",
                $"{student.FullName} bloklandi" +
                (string.IsNullOrWhiteSpace(model.Reason) ? "." : $" — {model.Reason}"),
                "danger");
        }

        public async Task UnblockAsync(int studentId)
        {
            var student = await _db.Students.FindAsync(studentId);
            if (student == null) return;

            student.Status = StudentStatus.Active;

            var blocked = await _db.BlockedUsers.FirstOrDefaultAsync(b => b.StudentId == studentId);
            if (blocked != null)
                _db.BlockedUsers.Remove(blocked);

            await _db.SaveChangesAsync();
            await RefreshKnownFacesAsync();

            await SendAlertAsync(
                "Blok olib tashlandi",
                $"{student.FullName} blokdan chiqarildi.",
                "success");
        }
    }
}
