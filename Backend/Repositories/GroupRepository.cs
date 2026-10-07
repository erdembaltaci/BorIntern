using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Repositories;

// Group ile ilgili tüm veritabanı sorguları burada; GroupService bu sorguların nasıl
// yazıldığını bilmez, sadece "bana şunu bul/kaydet" der.
public class GroupRepository : IGroupRepository
{
    private readonly AppDbContext _context;

    public GroupRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Group?> GetByIdAsync(int id)
    {
        return await _context.Groups.FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<Group?> GetByIdIncludingDeletedAsync(int id)
    {
        return await _context.Groups.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<List<Group>> GetByIdsAsync(IEnumerable<int> ids)
    {
        var idList = ids.Distinct().ToList();
        return await _context.Groups.Where(g => idList.Contains(g.Id)).ToListAsync();
    }

    public async Task<(List<Group> Items, int TotalCount)> GetPagedByMentorIdAsync(int mentorId, int page, int pageSize, string? search)
    {
        var query = _context.Groups.Where(g => g.MentorId == mentorId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(g => g.Name.Contains(search));
        }

        return await query.ToPagedAsync(page, pageSize);
    }

    public async Task<bool> GroupNameExistsAsync(string groupName)
    {
        return await _context.Groups.AnyAsync(g => g.Name == groupName);
    }

    public async Task<(List<Group> Items, int TotalCount)> GetPagedAllAsync(int page, int pageSize, string? search)
    {
        var query = _context.Groups.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Admin listesinde grup adının yanında mentorun adı/e-postasıyla da aranabilsin.
            var matchingMentors = _context.Users
                .Where(u => u.FullName.Contains(search) || u.Email.Contains(search))
                .Select(u => u.Id);
            query = query.Where(g => g.Name.Contains(search) || matchingMentors.Contains(g.MentorId));
        }

        return await query.ToPagedAsync(page, pageSize);
    }

    public async Task AddAsync(Group group)
    {
        await _context.Groups.AddAsync(group);
    }

    public async Task UpdateGroupAsync(Group group)
    {
        _context.Groups.Update(group);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
