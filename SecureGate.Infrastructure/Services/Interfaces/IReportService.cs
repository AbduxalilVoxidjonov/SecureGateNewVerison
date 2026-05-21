using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Interfaces
{
    public interface IReportService
    {
        Task<ReportViewModel> GetReportDataAsync();
    }
}
