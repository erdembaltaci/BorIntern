using Backend.Dtos;

namespace Backend.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto registerRequest);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto loginRequest);
}