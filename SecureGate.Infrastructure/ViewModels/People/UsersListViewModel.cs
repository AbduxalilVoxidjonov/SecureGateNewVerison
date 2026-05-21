using SecureGate.Domain;

namespace SecureGate.Infrastructure.ViewModels.People
{
    // ==================== STUDENT ====================
    public class UsersListViewModel
    {
        public List<Users> Students { get; set; } = new();
        public string? SearchTerm { get; set; }
        public int? GroupId { get; set; }
        public StudentStatus? StatusFilter { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
