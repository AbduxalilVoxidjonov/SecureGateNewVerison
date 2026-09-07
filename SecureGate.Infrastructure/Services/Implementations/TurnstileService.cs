using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain;
using SecureGate.Infrastructure.Hubs;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class TurnstileService : ITurnstileService
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<TurnstileHub> _turnstileHub;
        private readonly ILogger<TurnstileService> _logger;

        public TurnstileService(
            AppDbContext db,
            IHubContext<TurnstileHub> turnstileHub,
            ILogger<TurnstileService> logger)
        {
            _db = db;
            _turnstileHub = turnstileHub;
            _logger = logger;
        }

        /// <summary>
        /// TurnstileHub → "TurnstileStatusChanged" (id, status) va "TurnstileLog"
        /// (id, message, timeUtc). Frontend IKKI ALOHIDA argument kutadi — obyekt EMAS.
        ///
        /// <para>
        /// Broadcast HECH QACHON asosiy oqimni buzmaydi: SignalR xatosi faqat
        /// LogWarning ga yoziladi, turniket holati baribir o'zgargan bo'ladi.
        /// </para>
        /// </summary>
        private async Task NotifyStatusAsync(int id, TurnstileStatus status, string logMessage)
        {
            try
            {
                await _turnstileHub.Clients.All.SendAsync(
                    "TurnstileStatusChanged", id, status.ToString());

                await _turnstileHub.Clients.All.SendAsync(
                    "TurnstileLog", id, logMessage, DateTime.UtcNow.ToString("O"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "TurnstileStatusChanged SignalR event yuborishda xato (turniket #{Id})", id);
            }
        }

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

        /// <summary>
        /// Turniketni "ochilgan" holatga o'tkazadi.
        ///
        /// <para>
        /// <b>DIQQAT — HAL QILINMAGAN FUNKSIONAL BO'SHLIQ:</b> bu metod HOZIRCHA FAQAT
        /// ma'lumotlar bazasidagi holatni (<see cref="Turnstile.Status"/> va
        /// <see cref="Turnstile.LastActivityTime"/>) yangilaydi. Jismoniy qurilmaga
        /// (ZKTeco/Hikvision turniket kontrolleri) HECH QANDAY TCP/IP buyruq YUBORILMAYDI —
        /// qurilma protokoli hali aniqlanmagan. Ya'ni bu chaqiruvdan keyin turniket
        /// haqiqatda ochilmaydi; faqat tizimdagi yozuv o'zgaradi.
        /// </para>
        /// </summary>
        /// <returns>
        /// Turniket topilmasa yoki bloklangan bo'lsa <c>false</c>; aks holda <c>true</c>
        /// (DB holati yangilandi degani, qurilma ochildi degani EMAS).
        /// </returns>
        public async Task<bool> OpenAsync(int id)
        {
            var t = await _db.Turnstiles.FindAsync(id);
            if (t == null) return false;

            // Bloklangan turniket hech qanday holatda ochilmasligi kerak —
            // blokni faqat UnblockAsync olib tashlaydi.
            if (t.Status == TurnstileStatus.Blocked) return false;

            t.Status = TurnstileStatus.Online;
            t.LastActivityTime = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await NotifyStatusAsync(id, t.Status, "Turniket ochildi");

            // HAL QILINMAGAN: jismoniy qurilmaga ochish buyrug'i yuborilmaydi.
            // Qurilma protokoli (ZKTeco SDK / Hikvision ISAPI / oddiy TCP) aniqlangach,
            // shu yerda buyruq yuborilishi va natijasi qaytarilishi kerak.
            return true;
        }

        public async Task<bool> CloseAsync(int id)
        {
            var t = await _db.Turnstiles.FindAsync(id);
            if (t == null) return false;
            t.Status = TurnstileStatus.Offline;
            await _db.SaveChangesAsync();

            await NotifyStatusAsync(id, t.Status, "Turniket yopildi");
            return true;
        }

        public async Task<bool> BlockAsync(int id)
        {
            var t = await _db.Turnstiles.FindAsync(id);
            if (t == null) return false;
            t.Status = TurnstileStatus.Blocked;
            await _db.SaveChangesAsync();

            await NotifyStatusAsync(id, t.Status, "Turniket bloklandi");
            return true;
        }

        public async Task<bool> UnblockAsync(int id)
        {
            var t = await _db.Turnstiles.FindAsync(id);
            if (t == null) return false;
            t.Status = TurnstileStatus.Online;
            await _db.SaveChangesAsync();

            await NotifyStatusAsync(id, t.Status, "Turniket blokdan chiqarildi");
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

            // Har bir turniket uchun alohida holat eventi + umumiy "EmergencyOpen"
            // (bu event ilgari TurnstileHub.EmergencyOpenAll dan yuborilardi).
            foreach (var t in turnstiles)
                await NotifyStatusAsync(t.Id, t.Status, "Favqulodda ochish");

            try
            {
                await _turnstileHub.Clients.All.SendAsync("EmergencyOpen");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EmergencyOpen SignalR event yuborishda xato");
            }
        }
    }
}
