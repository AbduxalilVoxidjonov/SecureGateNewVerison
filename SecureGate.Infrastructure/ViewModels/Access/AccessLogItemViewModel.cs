namespace SecureGate.Infrastructure.ViewModels.Access
{
    public class AccessLogItemViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Type { get; set; } = "good"; // good, deny, warn, info
    }
}
