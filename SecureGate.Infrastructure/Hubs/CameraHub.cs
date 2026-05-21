using Microsoft.AspNetCore.SignalR;

namespace SecureGate.Infrastructure.Hubs
{
    public class CameraHub : Hub
    {
        // Kamera holati o'zgarganda
        public async Task NotifyCameraStatus(int cameraId, string status)
        {
            await Clients.All.SendAsync("CameraStatusChanged", cameraId, status);
        }

        // Yuz aniqlanganda (AI serverdan keladi)
        public async Task NotifyFaceDetected(int cameraId, string name, double confidence, bool isUnknown)
        {
            await Clients.All.SendAsync("FaceDetected", new
            {
                cameraId,
                name,
                confidence,
                isUnknown,
                time = DateTime.UtcNow.ToString("HH:mm:ss")
            });
        }

        // Harakat aniqlanganda
        public async Task NotifyMotionDetected(int cameraId, string location)
        {
            await Clients.All.SendAsync("MotionDetected", new
            {
                cameraId,
                location,
                time = DateTime.UtcNow.ToString("HH:mm:ss")
            });
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected", "CameraHub ga ulandi");
            await base.OnConnectedAsync();
        }
    }
}