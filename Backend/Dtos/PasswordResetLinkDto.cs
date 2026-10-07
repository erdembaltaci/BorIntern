namespace Backend.Dtos;

// Yöneticinin bir kullanıcı için ürettiği parola sıfırlama bağlantısı. Ham anahtar yalnızca BU cevapta görünür;
// veritabanında sadece özeti saklanır, sonradan tekrar gösterilemez (kaybolursa yenisi üretilir).
public class PasswordResetLinkDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

// Ön yüzün giriş/şifremi unuttum sayfalarını doğru göstermesi için herkese açık, hassas olmayan ayarlar.
public class PublicConfigDto
{
    // false ise e-posta gönderimi kapalıdır: "Şifremi unuttum" yerine "yöneticine başvur" gösterilir.
    public bool EmailEnabled { get; set; }
}
