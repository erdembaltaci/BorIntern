using Backend.Dtos;
using Backend.Entities;
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

        return new GroupDto
        {
            Id = group.Id,
            Name = group.Name,
            MentorId = group.MentorId,
            CreatedAt = group.CreatedAt
        };
    }

    public async Task<List<GroupDto>> GetMyGroupsAsync(int mentorId)
    {
        var groups = await _groupRepository.GetByMentorIdAsync(mentorId);

        return groups.Select(g => new GroupDto
        {
            Id = g.Id,
            Name = g.Name,
            MentorId = g.MentorId,
            CreatedAt = g.CreatedAt
        }).ToList();
    }
}
