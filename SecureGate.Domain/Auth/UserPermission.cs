namespace SecureGate.Domain.Auth
{
    public class UserPermission
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }
        public Permission Permission { get; set; }
    }
}
