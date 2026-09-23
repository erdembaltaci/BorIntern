using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Backend.Controllers;

// Giriş yapmış herhangi bir kullanıcının KENDİ profiliyle ilgili işlemleri.
// (Başka kullanıcıları yönetmek AdminController'ın işi.)
[ApiController]
[Route("api/users")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _userService.GetProfileAsync(userId);
        return Ok(result);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequestDto request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _userService.UpdateProfileAsync(userId, request);
        return Ok(result);
    }
}
