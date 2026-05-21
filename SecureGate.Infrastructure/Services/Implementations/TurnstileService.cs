using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class TurnstileService : ITurnstileService
    {
        private readonly AppDbContext _db;
        public TurnstileService(AppDbContext db) => _db = db;

        public async Task<List<Turnstile>> GetAllAsync() =>
            await _db.Turnstiles.Include(t => t.LinkedCamera).OrderBy(t => t.Name).ToListAsync();

        public async Task<Turnstile?> GetByIdAsync(int id) =>
            await _db.Turnstiles.Include(t => t.LinkedCamera).Include(t => t.AccessLogs.OrderByDescending(a => a.Timestamp).Take(20)).FirstOrDefaultAsync(t => t.Id == id);

        public async Task<Turnstile> CreateAsync(TurnstileCreateViewModel model)
        {
            var turnstile = new Turnstile
            {
                Name = model.Name,
                Location = model.Location,
                IpAddress = model.IpAddress,
                Port = model.Port,
                Model = model.Model,
                Type = model.Type,
                Direction = model.Direction,
                LinkedCameraId = model.LinkedCameraId,
                FaceRecognitionEnabled = model.FaceRecognitionEnabled,
                RfidEnabled = model.RfidEnabled,
                QrCodeEnabled = model.QrCodeEnabled
            };
            _db.Turnstiles.Add(turnstile);
            await _db.SaveChangesAsync();
            return turnstile;
        }

        public async Task<bool> OpenAsync(int id)
        {
            var t = await _db.Turnstiles.FindAsync(id);
            if (t == null) return false;
            t.Status = TurnstileStatus.Online;
            t.LastActivityTime = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            // TODO: TCP/IP buyruq qurilmaga yuborish
            return true;
        }

        public async Task<bool> CloseAsync(int id)
        {
            var t = await _db.Turnstiles.FindAsync(id);
            if (t == null) return false;
            t.Status = TurnstileStatus.Offline;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> BlockAsync(int id)
        {
            var t = await _db.Turnstiles.FindAsync(id);
            if (t == null) return false;
            t.Status = TurnstileStatus.Blocked;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UnblockAsync(int id)
        {
            var t = await _db.Turnstiles.FindAsync(id);
            if (t == null) return false;
            t.Status = TurnstileStatus.Online;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task EmergencyOpenAllAsync()
        {
            var turnstiles = await _db.Turnstiles.Where(t => t.Status != TurnstileStatus.Offline).ToListAsync();
            foreach (var t in turnstiles)
            {
                t.Status = TurnstileStatus.Online;
                t.LastActivityTime = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
        }
    }
}
