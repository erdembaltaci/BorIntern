namespace Backend.Entities;

// Access token (JWT) kısa ömürlü (1 saat). RefreshToken, süresi dolunca parola tekrar
// girmeden yeni bir access token almak için kullanılır. IsRevoked=true olursa (logout ya da
// kullanımdan sonra rotation ile), o refresh token bir daha kullanılamaz - bu bizim "token
// iptali" ihtiyacımızı karşılıyor.
public class RefreshToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}
