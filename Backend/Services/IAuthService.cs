using Backend.Dtos;

namespace Backend.Services;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequestDto registerRequest);
    Task<UserDto> LoginAsync(LoginRequestDto loginRequest);
}