using SecureGate.Domain;

namespace SecureGate.Infrastructure.ViewModels.Dashboard
{
    // ==================== DASHBOARD ====================
    public class DashboardViewModel
    {
        public int ActiveStudentCount { get; set; }
        public int TodayPassCount { get; set; }
        public int ActiveCameraCount { get; set; }
        public int TotalCameraCount { get; set; }
        public int AlertCount { get; set; }
        public int NewAlertCount { get; set; }

        public List<int> HourlyPassData { get; set; } = new();
        public List<AccessLogItemViewModel> RecentActivity { get; set; } = new();
        public List<TurnstileStatViewModel> PopularTurnstiles { get; set; } = new();
        public List<Alert> RecentAlerts { get; set; } = new();
    }
}
