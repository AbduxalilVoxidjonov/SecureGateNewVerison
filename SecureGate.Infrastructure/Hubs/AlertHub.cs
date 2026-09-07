using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SecureGate.Infrastructure.Hubs
{
    /// <summary>
    /// Ogohlantirishlar hubi. Faqat autentifikatsiyadan o'tgan klientlar uchun.
    ///
    /// DIQQAT: Bu hubda server → klient eventlari uchun public metod YO'Q.
    /// "NewAlert" va "BlockedAccessAttempt" eventlari server tomondan
    /// IHubContext&lt;AlertHub&gt; orqali yuboriladi (FaceMatchHandler, UsersService).
    /// Klient ularni chaqira olmaydi — aks holda istalgan ulangan klient
    /// soxta xavfsizlik ogohlantirishi yubora olardi.
    ///
    /// Vaqt qiymatlari ISO-8601 UTC ("O") formatida yuboriladi — formatlash frontend ishi.
    /// Alert turi: info | warning | danger | success.
    /// </summary>
    [Authorize]
    public class AlertHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected", "AlertHub ga ulandi");
            await base.OnConnectedAsync();
        }
    }
}
