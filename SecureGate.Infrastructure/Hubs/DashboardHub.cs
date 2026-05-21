using Microsoft.AspNetCore.SignalR;

namespace SecureGate.Infrastructure.Hubs
{
    public class DashboardHub : Hub
    {
        // Dashboard statistikasini yangilash
        public async Task UpdateStats(int activeStudents, int todayPass, int activeCameras, int alerts)
        {
            await Clients.All.SendAsync("StatsUpdated", new
            {
                activeStudents,
                todayPass,
                activeCameras,
                alerts,
                time = DateTime.UtcNow.ToString("HH:mm:ss")
            });
        }

        // Yangi faollik qo'shish (live feed)
        public async Task AddActivity(string userName, string action, string type)
        {
            await Clients.All.SendAsync("NewActivity", new
            {
                userName,
                action,
                type,
                time = DateTime.UtcNow.ToString("HH:mm:ss")
            });
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected", "DashboardHub ga ulandi");
            await base.OnConnectedAsync();
        }
    }
}
