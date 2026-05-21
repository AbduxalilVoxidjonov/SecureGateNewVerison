using Microsoft.AspNetCore.SignalR;

namespace SecureGate.Infrastructure.Hubs
{
    public class AlertHub : Hub
    {
        // Yangi ogohlantirish
        public async Task SendAlert(string title, string message, string type)
        {
            await Clients.All.SendAsync("NewAlert", new
            {
                title,
                message,
                type, // info, warning, danger, success
                time = DateTime.UtcNow.ToString("HH:mm:ss")
            });
        }

        // Bloklangan kirish urinishi
        public async Task NotifyBlockedAccess(string userName, string turnstileName, string reason)
        {
            await Clients.All.SendAsync("BlockedAccessAttempt", new
            {
                userName,
                turnstileName,
                reason,
                time = DateTime.UtcNow.ToString("HH:mm:ss")
            });
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected", "AlertHub ga ulandi");
            await base.OnConnectedAsync();
        }
    }
}