using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SecureGate.Infrastructure.Hubs
{
    /// <summary>
    /// Dashboard live-feed hubi. Faqat autentifikatsiyadan o'tgan klientlar uchun.
    ///
    /// DIQQAT: Bu hubda server → klient eventlari uchun public metod YO'Q.
    /// "StatsUpdated" va "NewActivity" eventlari server tomondan
    /// IHubContext&lt;DashboardHub&gt; orqali yuboriladi (FaceMatchHandler).
    /// Klient ularni chaqira olmaydi — aks holda istalgan ulangan klient
    /// soxta statistika/faollik yubora olardi.
    ///
    /// Vaqt qiymatlari ISO-8601 UTC ("O") formatida yuboriladi — formatlash frontend ishi.
    /// </summary>
    [Authorize]
    public class DashboardHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected", "DashboardHub ga ulandi");
            await base.OnConnectedAsync();
        }
    }
}
