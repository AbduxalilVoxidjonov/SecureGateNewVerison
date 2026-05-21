using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardViewModel> GetDashboardDataAsync();
    }
}

