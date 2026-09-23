using Backend.Dtos;

namespace Backend.Services;

public interface IUserService
{
    Task<UserDto> ApproveUserAsync(int userId);
    Task<UserDto> DeactivateUserAsync(int userId);
    Task<List<UserDto>> GetAllUsersAsync();
    Task<List<UserDto>> GetPendingUsersAsync();

    // Kendi profilini görme/güncelleme (herhangi bir rol kullanabilir, sadece kendi kaydı için).
    Task<UserDto> GetProfileAsync(int userId);
    Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequestDto request);
}
