using SecureGate.Domain;

namespace SecureGate.Infrastructure.ViewModels.People
{
    public class UsersEditViewModel : UsersCreateViewModel
    {
        public int Id { get; set; }
        public string? StudentId { get; set; }
        public string? PhotoPath { get; set; }
        public StudentStatus Status { get; set; }
    }
}
