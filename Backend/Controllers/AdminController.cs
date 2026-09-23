using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Controllers;

// Bu controller'daki her endpoint sadece Admin rolüne açık.
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("approve-user/{userId}")]
    public async Task<IActionResult> ApproveUser(int userId)
    {
        var result = await _userService.ApproveUserAsync(userId);
        return Ok(result);
    }

    [HttpPost("deactivate-user/{userId}")]
    public async Task<IActionResult> DeactivateUser(int userId)
    {
        var result = await _userService.DeactivateUserAsync(userId);
        return Ok(result);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var result = await _userService.GetAllUsersAsync();
        return Ok(result);
    }

    [HttpGet("users/pending")]
    public async Task<IActionResult> GetPendingUsers()
    {
        var result = await _userService.GetPendingUsersAsync();
        return Ok(result);
    }
}
