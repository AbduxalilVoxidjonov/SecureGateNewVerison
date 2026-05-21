using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class UsersService : IUsersService
    {
        private readonly AppDbContext _db;
        private readonly IPhotoStorageService _photoStorage;
        private readonly IFaceRecognitionClient _faceClient;
        private readonly ILogger<UsersService> _logger;

        public UsersService(
            AppDbContext db,
            IPhotoStorageService photoStorage,
            IFaceRecognitionClient faceClient,
            ILogger<UsersService> logger)
        {
            _db = db;
            _photoStorage = photoStorage;
            _faceClient = faceClient;
            _logger = logger;
        }

        public async Task<UsersListViewModel> GetStudentsAsync(string? search, int? groupId, StudentStatus? status, int page, int pageSize)
        {
            var query = _db.Students.Include(s => s.BlockedUser).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => s.FirstName.Contains(search) || s.LastName.Contains(search) || s.StudentId.Contains(search) || (s.Phone != null && s.Phone.Contains(search)));

            
            if (status.HasValue)
                query = query.Where(s => s.Status == status);

            var total = await query.CountAsync();
            var students = await query.OrderByDescending(s => s.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new UsersListViewModel
            {
                Students = students,
                SearchTerm = search,
                GroupId = groupId,
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
            var lastStudent = await _db.Students.OrderByDescending(s => s.Id).FirstOrDefaultAsync();
            var nextId = (lastStudent?.Id ?? 0) + 1;

            // Rasm majburiy — yuzni tanish turniketda ishlashi uchun
            var photoPath = await _photoStorage.SavePhotoAsync(model.PhotoFile, model.CapturedPhotoBase64, "users");
            if (string.IsNullOrEmpty(photoPath))
                throw new InvalidOperationException("Foydalanuvchi rasmi yuklanishi shart.");

            var student = new Users
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
                StudentId = nextId.ToString("D4"),
                Status = StudentStatus.New
            };

            _db.Students.Add(student);
            await _db.SaveChangesAsync();

            // FaceData yozuvi — encoding Python face-worker servisi orqali hisoblanadi.
            // Python yo'q yoki yuz topilmagan bo'lsa, encoding null bo'lib qoladi
            // (admin keyinroq "Qayta hisoblash" tugmasi orqali sinab ko'rishi mumkin).
            var encoding = await _faceClient.ComputeEmbeddingAsync(photoPath);
            if (encoding == null)
                _logger.LogWarning("Foydalanuvchi {Id} uchun encoding hisoblanmadi (Python yo'q yoki yuz topilmadi)", student.Id);

            _db.FaceData.Add(new FaceData
            {
                StudentId = student.Id,
                ImagePath = photoPath,
                FaceEncoding = encoding != null ? JsonSerializer.Serialize(encoding) : null,
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
        }
    }
}
