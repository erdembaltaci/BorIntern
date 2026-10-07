using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Backend.Controllers;

// Grup duyuruları. Rota controller seviyesinde değil metot seviyesinde: iki farklı kök altında uç noktamız var
// (/api/groups/{id}/announcements ve /api/announcements/mine). Rol kontrolü [Authorize] ile, sahiplik/üyelik
// kontrolü Service katmanında yapılır.
[ApiController]
[Authorize]
public class AnnouncementController : ControllerBase
{
    private readonly IAnnouncementService _announcementService;

    public AnnouncementController(IAnnouncementService announcementService)
    {
        _announcementService = announcementService;
    }

    // Sadece grubun sahibi mentor duyuru yazabilir.
    [Authorize(Roles = "Mentor")]
    [HttpPost("api/groups/{groupId}/announcements")]
    public async Task<IActionResult> Create(int groupId, CreateAnnouncementRequestDto request)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _announcementService.CreateAsync(mentorId, groupId, request);
        return Created($"/api/groups/{groupId}/announcements/{result.Id}", result);
    }

    // Grubun mentoru ya da üyesi (Service'te kontrol edilir).
    [HttpGet("api/groups/{groupId}/announcements")]
    public async Task<IActionResult> GetGroupAnnouncements(int groupId, int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _announcementService.GetGroupAnnouncementsAsync(callerId, groupId, page, pageSize);
        return Ok(result);
    }

    // Üyesi olduğum tüm grupların duyuruları (stajyerin ana duyuru sayfası).
    [HttpGet("api/announcements/mine")]
    public async Task<IActionResult> GetMyAnnouncements(int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _announcementService.GetMyAnnouncementsAsync(userId, page, pageSize);
        return Ok(result);
    }

    [Authorize(Roles = "Mentor")]
    [HttpDelete("api/groups/{groupId}/announcements/{announcementId}")]
    public async Task<IActionResult> Delete(int groupId, int announcementId)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _announcementService.DeleteAsync(mentorId, groupId, announcementId);
        return NoContent();
    }
}
