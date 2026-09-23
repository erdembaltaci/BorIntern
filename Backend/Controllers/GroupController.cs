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
    public async Task<IActionResult> GetMyGroups()
    {
        var mentorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _groupService.GetMyGroupsAsync(mentorId);
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
    public async Task<IActionResult> GetMembers(int groupId)
    {
        var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _groupMemberService.GetGroupMembersAsync(callerId, groupId);
        return Ok(result);
    }
}
