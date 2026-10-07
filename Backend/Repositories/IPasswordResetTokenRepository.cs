using Backend.Entities;

namespace Backend.Repositories;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token);

    // Ham anahtarın SHA-256 özetiyle arar (ham değer veritabanında yok).
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash);

    // Kullanıcının henüz kullanılmamış tüm bağlantılarını geçersiz kılar: "yeniden iste" dendiğinde sadece son bağlantı çalışsın.
    Task InvalidateActiveForUserAsync(int userId, DateTime utcNow);

    Task SaveChangesAsync();
}
