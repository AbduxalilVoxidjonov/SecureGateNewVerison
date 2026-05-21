using SecureGate.Domain;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Interfaces
{
    public interface IStaffService
    {
        Task<List<Staff>> GetAllAsync();
        Task<Staff?> GetByIdAsync(int id);
        Task<Staff> CreateAsync(StaffCreateViewModel model);
        Task<bool> UpdateAsync(StaffEditViewModel model);
        Task DeleteAsync(int id);
    }
}
