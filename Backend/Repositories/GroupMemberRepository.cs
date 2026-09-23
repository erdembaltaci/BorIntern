using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Repositories;

public class GroupMemberRepository : IGroupMemberRepository
{
    private readonly AppDbContext _context;

    public GroupMemberRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsUserInGroupAsync(int groupId, int userId)
    {
        return await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
    }

    public async Task<List<GroupMember>> GetByGroupIdAsync(int groupId)
    {
        return await _context.GroupMembers.Where(gm => gm.GroupId == groupId).ToListAsync();
    }

    public async Task<GroupMember?> GetByGroupAndUserAsync(int groupId, int userId)
    {
        return await _context.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
    }

    // Önce bu mentor'a ait grupların Id'lerini buluyoruz, sonra bu kullanıcının
    // o gruplardan herhangi birinde üye olup olmadığına bakıyoruz.
    public async Task<bool> IsUserInMentorGroupAsync(int mentorId, int userId)
    {
        var mentorGroupIds = _context.Groups.Where(g => g.MentorId == mentorId).Select(g => g.Id);
        return await _context.GroupMembers.AnyAsync(gm => gm.UserId == userId && mentorGroupIds.Contains(gm.GroupId));
    }

    public async Task AddAsync(GroupMember groupMember)
    {
        await _context.GroupMembers.AddAsync(groupMember);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
