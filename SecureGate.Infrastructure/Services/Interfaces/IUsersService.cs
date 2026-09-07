using SecureGate.Domain;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Interfaces
{
    public interface IUsersService
    {
        // DIQQAT: `groupId` parametri OLIB TASHLANDI — Users (o'quvchi) entity'sida guruh
        // maydoni yo'q, shuning uchun filtr hech qachon qo'llanmasdi (jim ishlamaydigan filtr).
        Task<UsersListViewModel> GetStudentsAsync(string? search, StudentStatus? status, int page, int pageSize);
        Task<Users?> GetByIdAsync(int id);
        Task<Users> CreateAsync(UsersCreateViewModel model);
        Task UpdateAsync(int id, UsersEditViewModel model);
        Task DeleteAsync(int id);
        Task BlockAsync(int studentId, BlockUserViewModel model);
        Task UnblockAsync(int studentId);
    }
}
