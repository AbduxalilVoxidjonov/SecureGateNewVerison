using Microsoft.AspNetCore.SignalR;
using SecureGate.Infrastructure.Services;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Hubs
{
    public class TurnstileHub : Hub
    {
        private readonly ITurnstileService _turnstileService;

        public TurnstileHub(ITurnstileService turnstileService)
        {
            _turnstileService = turnstileService;
        }

        // Client chaqiradi: turniketni ochish
        public async Task OpenTurnstile(int turnstileId)
        {
            var result = await _turnstileService.OpenAsync(turnstileId);
            if (result)
            {
                await Clients.All.SendAsync("TurnstileStatusChanged", turnstileId, "Online");
                await Clients.All.SendAsync("TurnstileLog", turnstileId, "Turniket ochildi", DateTime.UtcNow.ToString("HH:mm:ss"));
            }
        }

        // Client chaqiradi: turniketni yopish
        public async Task CloseTurnstile(int turnstileId)
        {
            var result = await _turnstileService.CloseAsync(turnstileId);
            if (result)
            {
                await Clients.All.SendAsync("TurnstileStatusChanged", turnstileId, "Offline");
                await Clients.All.SendAsync("TurnstileLog", turnstileId, "Turniket yopildi", DateTime.UtcNow.ToString("HH:mm:ss"));
            }
        }

        // Client chaqiradi: turniketni bloklash
        public async Task BlockTurnstile(int turnstileId)
        {
            var result = await _turnstileService.BlockAsync(turnstileId);
            if (result)
            {
                await Clients.All.SendAsync("TurnstileStatusChanged", turnstileId, "Blocked");
                await Clients.All.SendAsync("TurnstileLog", turnstileId, "Turniket bloklandi", DateTime.UtcNow.ToString("HH:mm:ss"));
            }
        }

        // Favqulodda: hammasini ochish
        public async Task EmergencyOpenAll()
        {
            await _turnstileService.EmergencyOpenAllAsync();
            await Clients.All.SendAsync("EmergencyOpen");
        }

        // O'tish hodisasi (server tomondan chaqiriladi — webhook yoki qurilma SDK)
        public async Task NotifyPassage(int turnstileId, string userName, string method, string result)
        {
            await Clients.All.SendAsync("PassageEvent", new
            {
                turnstileId,
                userName,
                method,
                result,
                time = DateTime.UtcNow.ToString("HH:mm:ss")
            });
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected", "TurnstileHub ga ulandi");
            await base.OnConnectedAsync();
        }
    }
}