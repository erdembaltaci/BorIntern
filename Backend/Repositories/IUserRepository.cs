using Backend.Entities;

namespace Backend.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
    Task<bool> EmailExistsAsync(string email);
    Task<List<User>> GetAllAsync();
    Task<List<User>> GetByStatusAsync(UserStatus status);
    Task AddAsync(User user);
    Task SaveChangesAsync();
}
