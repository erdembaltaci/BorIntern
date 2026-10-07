using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Repositories;

public class AnnouncementRepository : IAnnouncementRepository
{
    private readonly AppDbContext _context;

    public AnnouncementRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Announcement?> GetByIdAsync(int id)
    {
        return await _context.Announcements.FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<(List<Announcement> Items, int TotalCount)> GetPagedByGroupIdAsync(int groupId, int page, int pageSize)
    {
        return await _context.Announcements.Where(a => a.GroupId == groupId).ToPagedNewestFirstAsync(page, pageSize);
    }

    // Üyesi olduğu gruplar bulunur, sonra bu grupların duyuruları alınır. Silinmiş (soft delete) grupların
    // duyuruları görünmesin diye gruplar tablosunda da (query filter'lı) varlık kontrolü yapılır.
    public async Task<(List<Announcement> Items, int TotalCount)> GetPagedForMemberAsync(int userId, int page, int pageSize)
    {
        var memberGroupIds = _context.GroupMembers.Where(gm => gm.UserId == userId).Select(gm => gm.GroupId);

        return await _context.Announcements
            .Where(a => memberGroupIds.Contains(a.GroupId) && _context.Groups.Any(g => g.Id == a.GroupId))
            .ToPagedNewestFirstAsync(page, pageSize);
    }

    public async Task AddAsync(Announcement announcement)
    {
        await _context.Announcements.AddAsync(announcement);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
