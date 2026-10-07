using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;

namespace Backend.Services;

public class GroupService : IGroupService
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;

    public GroupService(IGroupRepository groupRepository, IUserRepository userRepository)
    {
        _groupRepository = groupRepository;
        _userRepository = userRepository;
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

        return await MapOneAsync(group);
    }

    public async Task<PagedResultDto<GroupDto>> GetMyGroupsAsync(int mentorId, int page, int pageSize, string? search)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (groups, totalCount) = await _groupRepository.GetPagedByMentorIdAsync(mentorId, page, pageSize, Pagination.NormalizeSearch(search));
        return PagedResultDto<GroupDto>.Create(await MapAllAsync(groups), page, pageSize, totalCount);
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

        return await MapOneAsync(group);
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

        return await MapOneAsync(group);
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

        return await MapOneAsync(group);
    }

    public async Task<PagedResultDto<GroupDto>> GetAllGroupsAsync(int page, int pageSize, string? search)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (groups, totalCount) = await _groupRepository.GetPagedAllAsync(page, pageSize, Pagination.NormalizeSearch(search));
        return PagedResultDto<GroupDto>.Create(await MapAllAsync(groups), page, pageSize, totalCount);
    }

    // Mentor adları, grup başına ayrı sorgu atmamak için tek seferde (toplu) getirilir.
    private async Task<List<GroupDto>> MapAllAsync(List<Group> groups)
    {
        var mentors = (await _userRepository.GetByIdsAsync(groups.Select(g => g.MentorId))).ToDictionary(u => u.Id, u => u.FullName);
        return groups.Select(g => MapToDto(g, mentors)).ToList();
    }

    private async Task<GroupDto> MapOneAsync(Group group)
    {
        return (await MapAllAsync(new List<Group> { group }))[0];
    }

    private static GroupDto MapToDto(Group group, Dictionary<int, string> mentorNames)
    {
        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name,
            MentorId = group.MentorId,
            MentorName = mentorNames.GetValueOrDefault(group.MentorId, string.Empty),
            CreatedAt = group.CreatedAt
        };
    }
}
