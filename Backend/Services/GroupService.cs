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
            throw new InvalidOperationException("Bu grup adı zaten kullanılıyor.");
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

    public async Task<List<GroupDto>> GetMyGroupsAsync(int mentorId)
    {
        var groups = await _groupRepository.GetByMentorIdAsync(mentorId);
        return groups.Select(MapToDto).ToList();
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
            throw new InvalidOperationException("Bu grup adı zaten kullanılıyor.");
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

    public async Task<List<GroupDto>> GetAllGroupsAsync()
    {
        var groups = await _groupRepository.GetAllGroupsAsync();
        return groups.Select(MapToDto).ToList();
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
