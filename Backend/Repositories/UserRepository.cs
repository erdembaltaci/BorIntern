using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Repositories;

// Tüm User veritabanı sorguları burada toplanır. Service katmanı, SQL/LINQ detayını bilmez.
public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }

    public async Task<(List<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
    {
        return await WithSearch(_context.Users, search).ToPagedAsync(page, pageSize);
    }

    // Admin ekranında "onay bekleyenler" gibi listeler için: duruma göre filtreli sorgu.
    public async Task<(List<User> Items, int TotalCount)> GetPagedByStatusAsync(UserStatus status, int page, int pageSize, string? search)
    {
        return await WithSearch(_context.Users.Where(u => u.Status == status), search).ToPagedAsync(page, pageSize);
    }

    // Arama metni varsa ad veya e-postada geçenleri bırakır (büyük/küçük harf duyarsız: SQL Server varsayılan collation).
    private static IQueryable<User> WithSearch(IQueryable<User> query, string? search)
    {
        return string.IsNullOrWhiteSpace(search)
            ? query
            : query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
    }

    public async Task<List<User>> GetByIdsAsync(IEnumerable<int> ids)
    {
        var idList = ids.Distinct().ToList();
        return await _context.Users.Where(u => idList.Contains(u.Id)).ToListAsync();
    }

    public async Task<(List<User> Items, int TotalCount)> GetPagedActiveInternsAsync(string? search, int page, int pageSize)
    {
        var query = _context.Users.Where(u => u.Role == UserRole.Intern && u.Status == UserStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.FullName.Contains(term) || u.Email.Contains(term));
        }

        return await query.ToPagedAsync(page, pageSize);
    }

    public async Task<(List<User> Items, int TotalCount)> GetPagedMentorInternsAsync(int mentorId, string? search, int page, int pageSize)
    {
        // Groups ve GroupMembers query filter'ları silinmiş (soft delete) grup/üyelikleri zaten gizler.
        var groupIds = _context.Groups.Where(g => g.MentorId == mentorId).Select(g => g.Id);
        var internIds = _context.GroupMembers.Where(gm => groupIds.Contains(gm.GroupId)).Select(gm => gm.UserId);

        var query = _context.Users.Where(u => internIds.Contains(u.Id) && u.Status == UserStatus.Active);
        return await WithSearch(query, search).ToPagedAsync(page, pageSize);
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesTranslatingConflictsAsync();
    }
}
