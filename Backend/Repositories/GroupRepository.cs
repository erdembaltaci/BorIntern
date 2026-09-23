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

    public async Task<List<Group>> GetByMentorIdAsync(int mentorId)
    {
        return await _context.Groups.Where(g => g.MentorId == mentorId).ToListAsync();
    }

    public async Task<bool> GroupNameExistsAsync(string groupName)
    {
        return await _context.Groups.AnyAsync(g => g.Name == groupName);
    }

    public async Task<List<Group>> GetAllGroupsAsync()
    {
        return await _context.Groups.ToListAsync();
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
