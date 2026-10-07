using Backend.Dtos;

namespace Backend.Services;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequestDto registerRequest);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto loginRequest);

    // Access token süresi dolunca, parola tekrar girmeden yeni bir çift (access+refresh) almak için.
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);

    // Giriş yapmış kullanıcı parolasını değiştirir: mevcut parola doğrulanır, TÜM refresh token'lar iptal edilir
    // (diğer cihazlardaki oturumlar kapanır) ve bu oturum için yeni bir token çifti döner.
    Task<AuthResponseDto> ChangePasswordAsync(int userId, ChangePasswordRequestDto request);

    // "Şifremi unuttum": kayıtlı ve aktif bir hesapsa tek kullanımlık bağlantıyı e-postayla yollar.
    // Hesap var olsun olmasın aynı şekilde sessizce döner (e-posta adreslerinin kayıtlı olup olmadığı sızmasın).
    Task ForgotPasswordAsync(ForgotPasswordRequestDto request);

    // E-postadaki bağlantıdaki anahtarla yeni parola belirler; anahtar tek kullanımlık ve 30 dakikalıktır.
    Task ResetPasswordAsync(ResetPasswordRequestDto request);

    // Refresh token'ı iptal eder - o andan sonra bu refresh token'la yeni access token alınamaz.
    Task LogoutAsync(RefreshTokenRequestDto request);
}
