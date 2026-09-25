using Backend.Dtos;

namespace Backend.Services;

public interface IUserService
{
    Task<UserDto> ApproveUserAsync(int userId);
    Task<UserDto> DeactivateUserAsync(int userId);
    Task<PagedResultDto<UserDto>> GetAllUsersAsync(int page, int pageSize);
    Task<PagedResultDto<UserDto>> GetPendingUsersAsync(int page, int pageSize);
    Task<UserDto> ChangeUserRoleAsync(int adminId, int userId, UpdateUserRoleRequestDto request);

    // Kendi profilini görme/güncelleme (herhangi bir rol kullanabilir, sadece kendi kaydı için).
    Task<UserDto> GetProfileAsync(int userId);
    Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequestDto request);
}
