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
    private readonly IGroupService _groupService;
    private readonly ITaskService _taskService;

    public AdminController(IUserService userService, IGroupService groupService, ITaskService taskService)
    {
        _userService = userService;
        _groupService = groupService;
        _taskService = taskService;
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

    // Tekil kullanıcı görüntüleme - UserController'daki "me" ile AYNI servis metodunu kullanıyor
    // (GetProfileAsync sahiplik kontrolü yapmıyor zaten, sadece Id'ye göre buluyor - burada
    // Admin herhangi bir kullanıcıyı görebildiği için bu, tekrar yazmadan doğrudan uyuyor).
    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUserById(int userId)
    {
        var result = await _userService.GetProfileAsync(userId);
        return Ok(result);
    }

    // Admin, hangi mentor'a ait olursa olsun TÜM grupları görebilir - GroupController'daki
    // "mine" endpoint'inden farklı olarak burada mentor sahiplik filtresi yok, bilerek.
    [HttpGet("groups")]
    public async Task<IActionResult> GetAllGroups()
    {
        var result = await _groupService.GetAllGroupsAsync();
        return Ok(result);
    }

    // Admin, hangi kullanıcıya atanmış olursa olsun TÜM görevleri görebilir.
    [HttpGet("tasks")]
    public async Task<IActionResult> GetAllTasks()
    {
        var result = await _taskService.GetAllTasksAsync();
        return Ok(result);
    }
}
