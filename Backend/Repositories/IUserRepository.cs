using Backend.Entities;

namespace Backend.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
    Task<bool> EmailExistsAsync(string email);
    Task<(List<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize);
    Task<(List<User> Items, int TotalCount)> GetPagedByStatusAsync(UserStatus status, int page, int pageSize);
    Task AddAsync(User user);
    Task SaveChangesAsync();
}
