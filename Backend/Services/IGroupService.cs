using Backend.Dtos;

namespace Backend.Services;

public interface IGroupService
{
    Task<GroupDto> CreateGroupAsync(int mentorId, CreateGroupRequestDto request);
    Task<List<GroupDto>> GetMyGroupsAsync(int mentorId);

    // Tekil grup görüntüleme - sadece grubun sahibi mentor.
    Task<GroupDto> GetGroupByIdAsync(int mentorId, int groupId);
    Task<GroupDto> UpdateGroupNameAsync(int mentorId, int groupId, CreateGroupRequestDto request);
    Task DeleteGroupAsync(int mentorId, int groupId);
    Task<GroupDto> RestoreGroupAsync(int mentorId, int groupId);

    // Admin, tüm mentor'ların gruplarını görebilir (mentor sahiplik kontrolü YOK - bilerek).
    Task<List<GroupDto>> GetAllGroupsAsync();
}
