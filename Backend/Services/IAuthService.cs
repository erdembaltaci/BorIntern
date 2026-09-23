using Backend.Dtos;

namespace Backend.Services;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequestDto registerRequest);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto loginRequest);

    // Access token süresi dolunca, parola tekrar girmeden yeni bir çift (access+refresh) almak için.
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);

    // Refresh token'ı iptal eder - o andan sonra bu refresh token'la yeni access token alınamaz.
    Task LogoutAsync(RefreshTokenRequestDto request);
}
