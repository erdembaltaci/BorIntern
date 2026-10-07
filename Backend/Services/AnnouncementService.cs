using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;

namespace Backend.Services;

public class AnnouncementService : IAnnouncementService
{
    private readonly IAnnouncementRepository _announcementRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupMemberRepository _groupMemberRepository;
    private readonly IUserRepository _userRepository;

    public AnnouncementService(
        IAnnouncementRepository announcementRepository,
        IGroupRepository groupRepository,
        IGroupMemberRepository groupMemberRepository,
        IUserRepository userRepository)
    {
        _announcementRepository = announcementRepository;
        _groupRepository = groupRepository;
        _groupMemberRepository = groupMemberRepository;
        _userRepository = userRepository;
    }

    public async Task<AnnouncementDto> CreateAsync(int mentorId, int groupId, CreateAnnouncementRequestDto request)
    {
        var group = await GetOwnedGroupAsync(mentorId, groupId);

        var announcement = new Announcement
        {
            GroupId = groupId,
            MentorId = mentorId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim()
        };

        await _announcementRepository.AddAsync(announcement);
        await _announcementRepository.SaveChangesAsync();

        var mentor = await _userRepository.GetByIdAsync(mentorId);
        return MapToDto(announcement, group.Name, mentor?.FullName);
    }

    public async Task<PagedResultDto<AnnouncementDto>> GetGroupAnnouncementsAsync(int callerId, int groupId, int page, int pageSize)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null)
        {
            throw new NotFoundException("Grup bulunamadı.");
        }

        // Duyuruları grubun mentoru ya da grubun bir üyesi görebilir (üye listesindeki kuralın aynısı).
        bool isOwnerMentor = group.MentorId == callerId;
        bool isMember = await _groupMemberRepository.IsUserInGroupAsync(groupId, callerId);
        if (!isOwnerMentor && !isMember)
        {
            throw new ForbiddenException("Bu grubun duyurularını görme yetkiniz yok.");
        }

        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (items, totalCount) = await _announcementRepository.GetPagedByGroupIdAsync(groupId, page, pageSize);
        return await ToPagedDtoAsync(items, page, pageSize, totalCount);
    }

    public async Task<PagedResultDto<AnnouncementDto>> GetMyAnnouncementsAsync(int userId, int page, int pageSize)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (items, totalCount) = await _announcementRepository.GetPagedForMemberAsync(userId, page, pageSize);
        return await ToPagedDtoAsync(items, page, pageSize, totalCount);
    }

    public async Task DeleteAsync(int mentorId, int groupId, int announcementId)
    {
        await GetOwnedGroupAsync(mentorId, groupId);

        var announcement = await _announcementRepository.GetByIdAsync(announcementId);
        // Duyuru başka bir gruba aitse de "yok" gibi davran: adres üzerinden başka grubun duyurusuna dokunulamasın.
        if (announcement == null || announcement.GroupId != groupId)
        {
            throw new NotFoundException("Duyuru bulunamadı.");
        }

        announcement.IsDeleted = true;
        announcement.DeletedAt = DateTime.UtcNow;
        await _announcementRepository.SaveChangesAsync();
    }

    // Grup var mı + bu mentora mı ait: yazma işlemlerinde ortak sahiplik kontrolü.
    private async Task<Group> GetOwnedGroupAsync(int mentorId, int groupId)
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

        return group;
    }

    // Grup ve mentor adları, duyuru başına ayrı sorgu atmamak için tek seferde (toplu) getirilir.
    private async Task<PagedResultDto<AnnouncementDto>> ToPagedDtoAsync(
        List<Announcement> announcements, int page, int pageSize, int totalCount)
    {
        var groups = (await _groupRepository.GetByIdsAsync(announcements.Select(a => a.GroupId))).ToDictionary(g => g.Id);
        var mentors = (await _userRepository.GetByIdsAsync(announcements.Select(a => a.MentorId))).ToDictionary(u => u.Id);

        var items = announcements
            .Select(a => MapToDto(a, groups.GetValueOrDefault(a.GroupId)?.Name, mentors.GetValueOrDefault(a.MentorId)?.FullName))
            .ToList();

        return PagedResultDto<AnnouncementDto>.Create(items, page, pageSize, totalCount);
    }

    private static AnnouncementDto MapToDto(Announcement announcement, string? groupName, string? mentorName)
    {
        return new AnnouncementDto
        {
            Id = announcement.Id,
            GroupId = announcement.GroupId,
            GroupName = groupName ?? string.Empty,
            MentorId = announcement.MentorId,
            MentorName = mentorName ?? string.Empty,
            Title = announcement.Title,
            Content = announcement.Content,
            CreatedAt = announcement.CreatedAt
        };
    }
}
