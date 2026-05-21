namespace SecureGate.Infrastructure.ViewModels.Reports
{
    // ==================== REPORT ====================
    public class ReportViewModel
    {
        public int WeeklyPassCount { get; set; }
        public double AverageAttendance { get; set; }
        public int LateArrivals { get; set; }
        public int DeniedCount { get; set; }
        public List<int> WeeklyData { get; set; } = new();
    }
 
}
