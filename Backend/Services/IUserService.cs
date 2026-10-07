using Backend.Dtos;

namespace Backend.Services;

public interface IUserService
{
    Task<UserDto> ApproveUserAsync(int userId);
    Task<UserDto> DeactivateUserAsync(int userId);
    Task<PagedResultDto<UserDto>> GetAllUsersAsync(int page, int pageSize, string? search);
    Task<PagedResultDto<UserDto>> GetPendingUsersAsync(int page, int pageSize, string? search);
    Task<UserDto> ChangeUserRoleAsync(int adminId, int userId, UpdateUserRoleRequestDto request);

    // Mentor'un gruba eklemek üzere aktif stajyerleri ad veya e-posta ile araması.
    Task<PagedResultDto<UserDto>> SearchActiveInternsAsync(string? search, int page, int pageSize);

    // Mentor'un kendi gruplarındaki aktif stajyerler (ad/e-posta ile aranabilir): görev devrinde seçici olarak kullanılır.
    Task<PagedResultDto<UserDto>> SearchMyInternsAsync(int mentorId, string? search, int page, int pageSize);

    // Kendi profilini görme/güncelleme (herhangi bir rol kullanabilir, sadece kendi kaydı için).
    Task<UserDto> GetProfileAsync(int userId);
    Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequestDto request);
}
