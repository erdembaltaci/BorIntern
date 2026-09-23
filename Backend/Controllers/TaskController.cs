using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Backend.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TaskController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TaskController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    // Sadece Mentor, kendi grubundaki bir stajyere görev atayabilir.
    [Authorize(Roles = "Mentor")]
    [HttpPost]
    public async Task<IActionResult> CreateTask(CreateTaskRequestDto request)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.CreateTaskAsync(mentorId, request);
        return Created($"/api/tasks/{result.Id}", result);
    }

    // Herhangi bir giriş yapmış kullanıcı çağırabilir, ama sadece kendi görevini güncelleyebilir
    // (kontrol Service katmanında yapılıyor).
    [HttpPut("{taskId}/status")]
    public async Task<IActionResult> UpdateStatus(int taskId, UpdateTaskStatusRequestDto request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.UpdateTaskStatusAsync(userId, taskId, request);
        return Ok(result);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMyTasks()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.GetMyTasksAsync(userId);
        return Ok(result);
    }

    // Tekil görev görüntüleme - sahiplik kontrolü (atanan stajyer ya da oluşturan mentor) Service'te.
    [HttpGet("{taskId}")]
    public async Task<IActionResult> GetTaskById(int taskId)
    {
        var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.GetTaskByIdAsync(callerId, taskId);
        return Ok(result);
    }

    // Sadece görevi oluşturan mentor silebilir - soft delete.
    [Authorize(Roles = "Mentor")]
    [HttpDelete("{taskId}")]
    public async Task<IActionResult> DeleteTask(int taskId)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _taskService.DeleteTaskAsync(mentorId, taskId);
        return NoContent();
    }

    // Silinen bir görevi geri getirir.
    [Authorize(Roles = "Mentor")]
    [HttpPost("{taskId}/restore")]
    public async Task<IActionResult> RestoreTask(int taskId)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.RestoreTaskAsync(mentorId, taskId);
        return Ok(result);
    }

    // Mentor, kendi grubundaki bir stajyerin görev özetini (kaç Todo/InProgress/Completed) görür.
    [Authorize(Roles = "Mentor")]
    [HttpGet("summary/{userId}")]
    public async Task<IActionResult> GetSummary(int userId)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.GetPerformanceSummaryAsync(mentorId, userId);
        return Ok(result);
    }
}
