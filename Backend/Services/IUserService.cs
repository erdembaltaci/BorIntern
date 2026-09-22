using Backend.Dtos;

namespace Backend.Services;

public interface IUserService
{
    Task<UserDto>ApproveUserAsync(int userId);  
}