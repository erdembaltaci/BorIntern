using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;

namespace Backend.Services;

// Kullanıcı yönetimiyle ilgili iş kuralları (onaylama, pasifleştirme, listeleme, profil).
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto> ApproveUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("Kullanıcı bulunamadı.");
        }

        user.Status = UserStatus.Active;
        await _userRepository.SaveChangesAsync();

        return MapToDto(user);
    }

    // Pasifleştirme: soft delete değil, Status = Inactive (en baştaki tasarım kararımız buydu).
    public async Task<UserDto> DeactivateUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("Kullanıcı bulunamadı.");
        }

        user.Status = UserStatus.Inactive;
        await _userRepository.SaveChangesAsync();

        return MapToDto(user);
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(MapToDto).ToList();
    }

    public async Task<List<UserDto>> GetPendingUsersAsync()
    {
        var users = await _userRepository.GetByStatusAsync(UserStatus.Pending);
        return users.Select(MapToDto).ToList();
    }

    public async Task<UserDto> GetProfileAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("Kullanıcı bulunamadı.");
        }

        return MapToDto(user);
    }

    public async Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequestDto request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("Kullanıcı bulunamadı.");
        }

        // Sadece FullName güncelleniyor - Email/Role/Status kullanıcının kendi isteğiyle değişmemeli.
        user.FullName = request.FullName;
        await _userRepository.SaveChangesAsync();

        return MapToDto(user);
    }

    // User -> UserDto çevirimi birkaç metodda tekrar ettiği için tek bir yardımcı metoda taşındı.
    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            CreatedAt = user.CreatedAt
        };
    }
}
