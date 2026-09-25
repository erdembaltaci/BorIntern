using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;

namespace Backend.Services;

public class GroupService : IGroupService
{
    private readonly IGroupRepository _groupRepository;

    public GroupService(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    // mentorId Controller'da token'dan (ClaimTypes.NameIdentifier) okunup buraya geliyor -
    // istek body'sinden asla alınmıyor, yoksa bir mentor başkası adına grup açabilirdi.
    public async Task<GroupDto> CreateGroupAsync(int mentorId, CreateGroupRequestDto request)
    {
        bool nameExists = await _groupRepository.GroupNameExistsAsync(request.Name);
        if (nameExists)
        {
            throw new ConflictException("Bu grup adı zaten kullanılıyor.");
        }

        var group = new Group
        {
            Name = request.Name,
            MentorId = mentorId
        };

        await _groupRepository.AddAsync(group);
        await _groupRepository.SaveChangesAsync();

        return MapToDto(group);
    }

    public async Task<PagedResultDto<GroupDto>> GetMyGroupsAsync(int mentorId, int page, int pageSize)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (groups, totalCount) = await _groupRepository.GetPagedByMentorIdAsync(mentorId, page, pageSize);
        return PagedResultDto<GroupDto>.Create(groups.Select(MapToDto).ToList(), page, pageSize, totalCount);
    }

    public async Task<GroupDto> GetGroupByIdAsync(int mentorId, int groupId)
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

        return MapToDto(group);
    }

    public async Task<GroupDto> UpdateGroupNameAsync(int mentorId, int groupId, CreateGroupRequestDto request)
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

        bool nameExists = await _groupRepository.GroupNameExistsAsync(request.Name);
        if (nameExists && group.Name != request.Name)
        {
            throw new ConflictException("Bu grup adı zaten kullanılıyor.");
        }

        group.Name = request.Name;
        await _groupRepository.UpdateGroupAsync(group);
        await _groupRepository.SaveChangesAsync();

        return MapToDto(group);
    }

    public async Task DeleteGroupAsync(int mentorId, int groupId)
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

        // Fiziksel silme yok - GroupMember kayıtları da (Cascade FK ile) ayakta kalır,
        // sadece Group'un kendisi soft-delete edilir; global query filter sayesinde artık
        // listelerde görünmez.
        group.IsDeleted = true;
        group.DeletedAt = DateTime.UtcNow;
        await _groupRepository.SaveChangesAsync();
    }

    public async Task<GroupDto> RestoreGroupAsync(int mentorId, int groupId)
    {
        var group = await _groupRepository.GetByIdIncludingDeletedAsync(groupId);
        if (group == null)
        {
            throw new NotFoundException("Grup bulunamadı.");
        }

        if (group.MentorId != mentorId)
        {
            throw new ForbiddenException("Bu grup size ait değil.");
        }

        if (!group.IsDeleted)
        {
            throw new InvalidOperationException("Bu grup zaten silinmemiş.");
        }

        group.IsDeleted = false;
        group.DeletedAt = null;
        await _groupRepository.SaveChangesAsync();

        return MapToDto(group);
    }

    public async Task<PagedResultDto<GroupDto>> GetAllGroupsAsync(int page, int pageSize)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (groups, totalCount) = await _groupRepository.GetPagedAllAsync(page, pageSize);
        return PagedResultDto<GroupDto>.Create(groups.Select(MapToDto).ToList(), page, pageSize, totalCount);
    }

    private static GroupDto MapToDto(Group group)
    {
        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name,
            MentorId = group.MentorId,
            CreatedAt = group.CreatedAt
        };
    }
}
