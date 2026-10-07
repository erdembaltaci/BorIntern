using Backend.Dtos;

namespace Backend.Services;

public interface IAnnouncementService
{
    // Sadece grubun sahibi mentor duyuru yazabilir.
    Task<AnnouncementDto> CreateAsync(int mentorId, int groupId, CreateAnnouncementRequestDto request);

    // callerId: grubun mentoru ya da grubun bir üyesi olmalı.
    Task<PagedResultDto<AnnouncementDto>> GetGroupAnnouncementsAsync(int callerId, int groupId, int page, int pageSize);

    // Kullanıcının üyesi olduğu tüm grupların duyuruları.
    Task<PagedResultDto<AnnouncementDto>> GetMyAnnouncementsAsync(int userId, int page, int pageSize);

    // Sadece grubun sahibi mentor silebilir (soft delete).
    Task DeleteAsync(int mentorId, int groupId, int announcementId);
}
