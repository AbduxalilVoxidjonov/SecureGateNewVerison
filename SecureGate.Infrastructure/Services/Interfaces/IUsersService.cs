using SecureGate.Domain;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Interfaces
{
    public interface IUsersService
    {
        Task<UsersListViewModel> GetStudentsAsync(string? search, int? groupId, StudentStatus? status, int page, int pageSize);
        Task<Users?> GetByIdAsync(int id);
        Task<Users> CreateAsync(UsersCreateViewModel model);
        Task UpdateAsync(int id, UsersEditViewModel model);
        Task DeleteAsync(int id);
        Task BlockAsync(int studentId, BlockUserViewModel model);
        Task UnblockAsync(int studentId);
    }
}
