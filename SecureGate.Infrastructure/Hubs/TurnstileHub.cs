using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SecureGate.Infrastructure.Hubs
{
    /// <summary>
    /// Turniket hodisalari uchun SignalR hub. Faqat autentifikatsiyadan o'tgan klientlar uchun.
    ///
    /// DIQQAT: Bu hubda public metod YO'Q.
    /// <list type="bullet">
    /// <item>Turniketni ochish/yopish/bloklash — REST orqali
    /// (<c>TurnstilesController</c>), chunki u yerda rol/ruxsat tekshiruvi bor.
    /// Hubdagi eski <c>OpenTurnstile</c>/<c>CloseTurnstile</c>/<c>BlockTurnstile</c>/
    /// <c>EmergencyOpenAll</c> metodlari o'chirildi — ular istalgan ulangan klientga
    /// turniketni boshqarish va soxta event yuborish imkonini berardi.</item>
    /// <item>"TurnstileStatusChanged" (id, status), "TurnstileLog" (id, message, timeUtc)
    /// va "EmergencyOpen" eventlari server tomondan IHubContext&lt;TurnstileHub&gt;
    /// orqali yuboriladi (<c>TurnstileService</c>).</item>
    /// </list>
    ///
    /// "TurnstileStatusChanged" IKKI ALOHIDA argument bilan yuboriladi (obyekt emas):
    /// <c>(int id, string status)</c> — frontend shu shaklni kutadi.
    /// Vaqt qiymatlari ISO-8601 UTC ("O") formatida — formatlash frontend ishi.
    /// </summary>
    [Authorize]
    public class TurnstileHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected", "TurnstileHub ga ulandi");
            await base.OnConnectedAsync();
        }
    }
}
