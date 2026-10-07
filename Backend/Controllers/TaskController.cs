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

    // ?status=Todo gibi durum filtresi desteklenir (pano sütunları ve liste filtresi için).
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyTasks(int page = 1, int pageSize = Pagination.DefaultPageSize, string? status = null)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.GetMyTasksAsync(userId, page, pageSize, status);
        return Ok(result);
    }

    // Kendi görevlerimin durum sayıları ve geciken sayısı (görevleri çekmeden, veritabanında sayılır).
    [HttpGet("mine/summary")]
    public async Task<IActionResult> GetMySummary()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.GetMySummaryAsync(userId);
        return Ok(result);
    }

    // Panel için: bitiş tarihi en yakın bitmemiş görevler.
    [HttpGet("mine/upcoming")]
    public async Task<IActionResult> GetMyUpcoming(int take = 5)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.GetMyUpcomingTasksAsync(userId, take);
        return Ok(result);
    }

    // Mentor'un kendi atadığı görevlerin listesi (atanan stajyerin değil, atayan mentorun bakışı).
    [Authorize(Roles = "Mentor")]
    [HttpGet("created")]
    public async Task<IActionResult> GetCreatedTasks(int page = 1, int pageSize = Pagination.DefaultPageSize, string? search = null)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.GetCreatedTasksAsync(mentorId, page, pageSize, search);
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

    // Sadece görevi oluşturan mentor düzenleyebilir (sahiplik Service'te denetlenir).
    [Authorize(Roles = "Mentor")]
    [HttpPut("{taskId}")]
    public async Task<IActionResult> UpdateTask(int taskId, UpdateTaskRequestDto request)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.UpdateTaskAsync(mentorId, taskId, request);
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
