using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class StaffService : IStaffService
    {
        private readonly AppDbContext _db;
        private readonly IPhotoStorageService _photoStorage;
        private readonly IFaceRecognitionClient _faceClient;
        private readonly ILogger<StaffService> _logger;

        public StaffService(
            AppDbContext db,
            IPhotoStorageService photoStorage,
            IFaceRecognitionClient faceClient,
            ILogger<StaffService> logger)
        {
            _db = db;
            _photoStorage = photoStorage;
            _faceClient = faceClient;
            _logger = logger;
        }

        public async Task<List<Staff>> GetAllAsync() =>
            await _db.StaffMembers.OrderBy(s => s.FullName).ToListAsync();

        public async Task<Staff?> GetByIdAsync(int id) =>
            await _db.StaffMembers
                .Include(s => s.TurnstilePermissions).ThenInclude(tp => tp.Turnstile)
                .FirstOrDefaultAsync(s => s.Id == id);

        public async Task<Staff> CreateAsync(StaffCreateViewModel model)
        {
            var photoPath = await _photoStorage.SavePhotoAsync(model.PhotoFile, model.CapturedPhotoBase64, "staff");
            if (string.IsNullOrEmpty(photoPath))
                throw new InvalidOperationException("Xodim rasmi yuklanishi shart.");

            var staff = new Staff
            {
                FullName = model.FullName,
                Position = model.Position,
                Department = model.Department,
                Shift = model.Shift,
                Phone = model.Phone,
                AccessLevel = model.AccessLevel,
                PhotoPath = photoPath
            };
            _db.StaffMembers.Add(staff);
            await _db.SaveChangesAsync();

            // Encoding hisoblash (Python yo'q bo'lsa null bo'lib qoladi).
            var encoding = await _faceClient.ComputeEmbeddingAsync(photoPath);
            if (encoding == null)
                _logger.LogWarning("Xodim {Id} uchun encoding hisoblanmadi", staff.Id);

            _db.FaceData.Add(new FaceData
            {
                StaffId = staff.Id,
                ImagePath = photoPath,
                FaceEncoding = encoding != null ? JsonSerializer.Serialize(encoding) : null,
                ConfidenceLevel = FaceConfidenceLevel.High,
                IsActive = staff.FaceRecognitionEnabled
            });
            await _db.SaveChangesAsync();

            return staff;
        }

        public async Task<bool> UpdateAsync(StaffEditViewModel model)
        {
            var staff = await _db.StaffMembers.FindAsync(model.Id);
            if (staff == null) return false;

            staff.FullName = model.FullName;
            staff.Position = model.Position;
            staff.Department = model.Department;
            staff.Shift = model.Shift;
            staff.Phone = model.Phone;
            staff.AccessLevel = model.AccessLevel;
            staff.Status = model.Status;
            staff.FaceRecognitionEnabled = model.FaceRecognitionEnabled;

            var newPhotoPath = await _photoStorage.SavePhotoAsync(model.PhotoFile, model.CapturedPhotoBase64, "staff");
            if (!string.IsNullOrEmpty(newPhotoPath))
            {
                _photoStorage.DeletePhoto(staff.PhotoPath);
                staff.PhotoPath = newPhotoPath;

                // Yangi rasm → encoding qayta hisoblanadi
                var newEncoding = await _faceClient.ComputeEmbeddingAsync(newPhotoPath);
                var encodingJson = newEncoding != null ? JsonSerializer.Serialize(newEncoding) : null;

                var face = await _db.FaceData.FirstOrDefaultAsync(f => f.StaffId == staff.Id);
                if (face == null)
                {
                    _db.FaceData.Add(new FaceData
                    {
                        StaffId = staff.Id,
                        ImagePath = newPhotoPath,
                        FaceEncoding = encodingJson,
                        ConfidenceLevel = FaceConfidenceLevel.High,
                        IsActive = staff.FaceRecognitionEnabled
                    });
                }
                else
                {
                    face.ImagePath = newPhotoPath;
                    face.FaceEncoding = encodingJson;
                    face.IsActive = staff.FaceRecognitionEnabled;
                }
            }
            else
            {
                var face = await _db.FaceData.FirstOrDefaultAsync(f => f.StaffId == staff.Id);
                if (face != null) face.IsActive = staff.FaceRecognitionEnabled;
            }

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task DeleteAsync(int id)
        {
            var staff = await _db.StaffMembers.FindAsync(id);
            if (staff == null) return;

            var photoPath = staff.PhotoPath;
            _db.StaffMembers.Remove(staff);
            await _db.SaveChangesAsync();

            _photoStorage.DeletePhoto(photoPath);
        }
    }
}
