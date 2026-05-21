using SecureGate.Domain;

namespace SecureGate.Infrastructure.Services.Interfaces
{
    public interface ISettingService
    {
        Task<List<Setting>> GetAllAsync();
        Task<bool> GetBoolAsync(string key);
        Task<string?> GetValueAsync(string key, string? defaultValue = null);
        Task<Dictionary<string, string?>> GetManyAsync(IEnumerable<string> keys);
        Task SetAsync(string key, string? value, SettingType type = SettingType.String, string? description = null);
        Task SetManyAsync(IDictionary<string, string?> values);
    }
}
