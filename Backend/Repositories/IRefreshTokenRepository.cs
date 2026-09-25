using Backend.Entities;

namespace Backend.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task AddAsync(RefreshToken refreshToken);

    // Süresi dolmuş token'ları tek SQL DELETE ile siler, silinen adedi döner.
    Task<int> DeleteExpiredAsync(DateTime utcNow);
    Task SaveChangesAsync();
}
