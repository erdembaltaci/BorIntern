using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Backend.Controllers;

// Not: class seviyesinde [Authorize(Roles="Mentor")] KOYMUYORUZ, çünkü GetMembers
// hem mentor hem sıradan üyeler tarafından çağrılabilmeli. Her metod kendi yetkisini tanımlıyor.
[ApiController]
[Route("api/groups")]
public class GroupController : ControllerBase
{
    private readonly IGroupService _groupService;
    private readonly IGroupMemberService _groupMemberService;

    public GroupController(IGroupService groupService, IGroupMemberService groupMemberService)
    {
        _groupService = groupService;
        _groupMemberService = groupMemberService;
    }

    // Sadece Mentor yeni grup oluşturabilir.
    [Authorize(Roles = "Mentor")]
    [HttpPost]
    public async Task<IActionResult> CreateGroup(CreateGroupRequestDto request)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _groupService.CreateGroupAsync(mentorId, request);
        return Created($"/api/groups/{result.Id}", result);
    }

    // Mentor, kendi gruplarını listeler.
    [Authorize(Roles = "Mentor")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyGroups(int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _groupService.GetMyGroupsAsync(mentorId, page, pageSize);
        return Ok(result);
    }

    // Tekil grup görüntüleme - sadece grubun sahibi mentor.
    [Authorize(Roles = "Mentor")]
    [HttpGet("{groupId}")]
    public async Task<IActionResult> GetGroupById(int groupId)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _groupService.GetGroupByIdAsync(mentorId, groupId);
        return Ok(result);
    }

    // Bir mentor, sadece KENDİ grubuna üye ekleyebilir - sahiplik kontrolü Service içinde yapılıyor.
    [Authorize(Roles = "Mentor")]
    [HttpPost("{groupId}/members")]
    public async Task<IActionResult> AddMember(int groupId, AddGroupMemberRequestDto request)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _groupMemberService.AddMemberAsync(mentorId, groupId, request);
        return Ok(result);
    }

    // Üye listesini hem grubun mentoru hem de grubun üyeleri görebilir - rol şartı yok,
    // kimin görüp göremeyeceği kontrolü GroupMemberService içinde yapılıyor.
    [Authorize]
    [HttpGet("{groupId}/members")]
    public async Task<IActionResult> GetMembers(int groupId, int page = 1, int pageSize = Pagination.DefaultPageSize)
    {
        var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _groupMemberService.GetGroupMembersAsync(callerId, groupId, page, pageSize);
        return Ok(result);
    }

    // Sadece grubun mentoru bir üyeyi çıkarabilir.
    [Authorize(Roles = "Mentor")]
    [HttpDelete("{groupId}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(int groupId, int userId)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _groupMemberService.RemoveMemberAsync(mentorId, groupId, userId);
        return NoContent();
    }

    // Grup adını günceller - sadece grubun sahibi mentor.
    [Authorize(Roles = "Mentor")]
    [HttpPut("{groupId}")]
    public async Task<IActionResult> UpdateGroup(int groupId, CreateGroupRequestDto request)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _groupService.UpdateGroupNameAsync(mentorId, groupId, request);
        return Ok(result);
    }

    // Grubu soft-delete eder - fiziksel silme yok.
    [Authorize(Roles = "Mentor")]
    [HttpDelete("{groupId}")]
    public async Task<IActionResult> DeleteGroup(int groupId)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _groupService.DeleteGroupAsync(mentorId, groupId);
        return NoContent();
    }

    // Silinen bir grubu geri getirir.
    [Authorize(Roles = "Mentor")]
    [HttpPost("{groupId}/restore")]
    public async Task<IActionResult> RestoreGroup(int groupId)
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _groupService.RestoreGroupAsync(mentorId, groupId);
        return Ok(result);
    }
}
