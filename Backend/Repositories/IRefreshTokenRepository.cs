using Backend.Entities;

namespace Backend.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task AddAsync(RefreshToken refreshToken);

    // Kullanıcının TÜM refresh token'larını iptal eder (parola değişince/sıfırlanınca bütün cihazlardaki oturumlar kapanır).
    Task RevokeAllByUserIdAsync(int userId);

    // Süresi dolmuş token'ları tek SQL DELETE ile siler, silinen adedi döner.
    Task<int> DeleteExpiredAsync(DateTime utcNow);
    Task SaveChangesAsync();
}
