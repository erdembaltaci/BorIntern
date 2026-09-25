using Backend.Dtos;

namespace Backend.Services;

public interface IGroupMemberService
{
    Task<GroupMemberDto> AddMemberAsync(int mentorId, int groupId, AddGroupMemberRequestDto request);

    // callerId: isteği atan kişi - grubun mentoru ya da grubun bir üyesi olmalı.
    Task<PagedResultDto<GroupMemberDto>> GetGroupMembersAsync(int callerId, int groupId, int page, int pageSize);

    // Sadece grubun mentoru bir üyeyi çıkarabilir.
    Task RemoveMemberAsync(int mentorId, int groupId, int userId);
}
