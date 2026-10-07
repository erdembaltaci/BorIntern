using Backend.Entities;

namespace Backend.Repositories;

public interface IAnnouncementRepository
{
    Task<Announcement?> GetByIdAsync(int id);

    // Bir grubun duyuruları, en yeni önce.
    Task<(List<Announcement> Items, int TotalCount)> GetPagedByGroupIdAsync(int groupId, int page, int pageSize);

    // Bir kullanıcının ÜYE olduğu tüm grupların duyuruları, en yeni önce (stajyerin "Duyurular" sayfası).
    Task<(List<Announcement> Items, int TotalCount)> GetPagedForMemberAsync(int userId, int page, int pageSize);

    Task AddAsync(Announcement announcement);
    Task SaveChangesAsync();
}
