namespace Backend.Entities;

// "Şifremi unuttum" bağlantısındaki tek kullanımlık anahtar. Refresh token'da olduğu gibi ham değer veritabanına
// YAZILMAZ, sadece SHA-256 özeti (TokenHash) saklanır: veritabanı sızsa bile bağlantı üretilemez.
public class PasswordResetToken : BaseEntity
{
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    // Kullanıldıysa (ya da yenisi istenince geçersiz kılındıysa) dolar; bir daha kullanılamaz.
    public DateTime? UsedAt { get; set; }
}
