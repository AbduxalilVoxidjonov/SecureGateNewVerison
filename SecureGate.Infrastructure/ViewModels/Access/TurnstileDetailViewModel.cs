using SecureGate.Domain;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Access
{
    // ==================== TURNSTILE ====================
    public class TurnstileDetailViewModel
    {
        public Turnstile Turnstile { get; set; } = null!;
        public List<AccessLog> RecentLogs { get; set; } = new();
        public List<int> HourlyData { get; set; } = new();
    }
    
}
