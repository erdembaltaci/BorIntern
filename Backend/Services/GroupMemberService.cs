using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;

namespace Backend.Services;

public class GroupMemberService : IGroupMemberService
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupMemberRepository _groupMemberRepository;

    public GroupMemberService(IGroupRepository groupRepository, IGroupMemberRepository groupMemberRepository)
    {
        _groupRepository = groupRepository;
        _groupMemberRepository = groupMemberRepository;
    }

    public async Task<GroupMemberDto> AddMemberAsync(int mentorId, int groupId, AddGroupMemberRequestDto request)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null)
        {
            throw new NotFoundException("Grup bulunamadı.");
        }

        // Mentor sahiplik kontrolü: bu grup gerçekten bu mentor'a mı ait.
        if (group.MentorId != mentorId)
        {
            throw new ForbiddenException("Bu grup size ait değil.");
        }

        bool alreadyMember = await _groupMemberRepository.IsUserInGroupAsync(groupId, request.UserId);
        if (alreadyMember)
        {
            throw new ConflictException("Bu kullanıcı zaten grubun üyesi.");
        }

        var groupMember = new GroupMember
        {
            GroupId = groupId,
            UserId = request.UserId
        };

        await _groupMemberRepository.AddAsync(groupMember);
        await _groupMemberRepository.SaveChangesAsync();

        return MapToDto(groupMember);
    }

    public async Task<PagedResultDto<GroupMemberDto>> GetGroupMembersAsync(int callerId, int groupId, int page, int pageSize)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null)
        {
            throw new NotFoundException("Grup bulunamadı.");
        }

        // Üye listesini görebilmek için ya grubun mentoru ya da grubun bir üyesi olmak gerekiyor.
        bool isOwnerMentor = group.MentorId == callerId;
        bool isMember = await _groupMemberRepository.IsUserInGroupAsync(groupId, callerId);

        if (!isOwnerMentor && !isMember)
        {
            throw new ForbiddenException("Bu grubun üyelerini görme yetkiniz yok.");
        }

        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (members, totalCount) = await _groupMemberRepository.GetPagedByGroupIdAsync(groupId, page, pageSize);
        return PagedResultDto<GroupMemberDto>.Create(members.Select(MapToDto).ToList(), page, pageSize, totalCount);
    }

    public async Task RemoveMemberAsync(int mentorId, int groupId, int userId)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null)
        {
            throw new NotFoundException("Grup bulunamadı.");
        }

        if (group.MentorId != mentorId)
        {
            throw new ForbiddenException("Bu grup size ait değil.");
        }

        var membership = await _groupMemberRepository.GetByGroupAndUserAsync(groupId, userId);
        if (membership == null)
        {
            throw new NotFoundException("Bu kullanıcı grubun üyesi değil.");
        }

        // Fiziksel silme yok - GroupMember de SoftDeletableEntity, aynı desen burada da geçerli.
        membership.IsDeleted = true;
        membership.DeletedAt = DateTime.UtcNow;
        await _groupMemberRepository.SaveChangesAsync();
    }

    private static GroupMemberDto MapToDto(GroupMember groupMember)
    {
        return new GroupMemberDto
        {
            Id = groupMember.Id,
            GroupId = groupMember.GroupId,
            UserId = groupMember.UserId,
            JoinedAt = groupMember.JoinedAt
        };
    }
}
