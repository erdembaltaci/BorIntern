using Backend.Dtos;

namespace Backend.Services;

public interface IGroupService
{
    Task<GroupDto> CreateGroupAsync(int mentorId, CreateGroupRequestDto request);
    Task<List<GroupDto>> GetMyGroupsAsync(int mentorId);
}
