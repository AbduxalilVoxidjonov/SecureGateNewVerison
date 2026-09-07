using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SecureGate.Infrastructure.Hubs
{
    /// <summary>
    /// Kamera hodisalari uchun SignalR hub.
    /// Faqat autentifikatsiyadan o'tgan klientlar ulanishi mumkin.
    ///
    /// DIQQAT: Bu hubda server → klient eventlari uchun public metod YO'Q.
    /// "FaceDetected", "CameraStatusChanged", "MotionDetected", "NewSighting",
    /// "NewAccessLog", "FaceFrameProcessed" eventlari server tomondan
    /// IHubContext&lt;CameraHub&gt; orqali yuboriladi (CameraStreamWorker,
    /// FaceMatchHandler, CameraSightingHandler). Klient ularni chaqira olmaydi.
    /// </summary>
    [Authorize]
    public class CameraHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected", "CameraHub ga ulandi");
            await base.OnConnectedAsync();
        }
    }
}
