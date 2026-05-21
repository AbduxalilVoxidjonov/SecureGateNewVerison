using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class SettingService : ISettingService
    {
        private readonly AppDbContext _db;
        public SettingService(AppDbContext db) => _db = db;

        public async Task<List<Setting>> GetAllAsync() =>
            await _db.Settings.ToListAsync();

        public async Task<bool> GetBoolAsync(string key)
        {
            var s = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key);
            return string.Equals(s?.Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string?> GetValueAsync(string key, string? defaultValue = null)
        {
            var s = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key);
            return s?.Value ?? defaultValue;
        }

        public async Task<Dictionary<string, string?>> GetManyAsync(IEnumerable<string> keys)
        {
            var keyList = keys.ToList();
            var rows = await _db.Settings.Where(s => keyList.Contains(s.Key)).ToListAsync();
            var dict = keyList.ToDictionary(k => k, k => (string?)null);
            foreach (var r in rows) dict[r.Key] = r.Value;
            return dict;
        }

        public async Task SetAsync(string key, string? value, SettingType type = SettingType.String, string? description = null)
        {
            var s = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key);
            if (s == null)
            {
                _db.Settings.Add(new Setting
                {
                    Key = key,
                    Value = value,
                    Type = type,
                    Description = description
                });
            }
            else
            {
                s.Value = value;
                if (description != null) s.Description = description;
            }
            await _db.SaveChangesAsync();
        }

        public async Task SetManyAsync(IDictionary<string, string?> values)
        {
            var keys = values.Keys.ToList();
            var existing = await _db.Settings.Where(s => keys.Contains(s.Key)).ToListAsync();
            var existingByKey = existing.ToDictionary(s => s.Key);

            foreach (var (key, value) in values)
            {
                if (existingByKey.TryGetValue(key, out var s))
                {
                    s.Value = value;
                }
                else
                {
                    _db.Settings.Add(new Setting { Key = key, Value = value, Type = SettingType.String });
                }
            }
            await _db.SaveChangesAsync();
        }
    }
}
