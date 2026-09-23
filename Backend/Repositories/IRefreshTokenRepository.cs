using Backend.Entities;

namespace Backend.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task AddAsync(RefreshToken refreshToken);
    Task SaveChangesAsync();
}
