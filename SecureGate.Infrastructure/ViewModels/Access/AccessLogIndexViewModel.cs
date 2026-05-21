using SecureGate.Domain;

namespace SecureGate.Infrastructure.ViewModels.Access
{
    // ==================== ACCESS LOG INDEX ====================
    public class AccessLogIndexViewModel
    {
        // Jurnal yozuvlari
        public List<AccessLog> Logs { get; set; } = new();

        // Filtrlar
        public string? SearchTerm { get; set; }
        public AccessResult? ResultFilter { get; set; }
        public AccessMethod? MethodFilter { get; set; }
        public int? TurnstileId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // Filtr uchun ro'yxat (dropdown)
        public List<Turnstile> Turnstiles { get; set; } = new();

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
