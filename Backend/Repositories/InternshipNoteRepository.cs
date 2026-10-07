using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Repositories;

public class InternshipNoteRepository : IInternshipNoteRepository
{
    private readonly AppDbContext _context;

    public InternshipNoteRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<InternshipNote?> GetByIdAsync(int id)
    {
        return await _context.InternshipNotes.FirstOrDefaultAsync(n => n.Id == id);
    }

    // IgnoreQueryFilters(): AppDbContext'teki "!IsDeleted" filtresini bu sorgu için devre dışı
    // bırakır - yoksa silinmiş bir notu asla bulamayız, restore etmek imkansız olurdu.
    public async Task<InternshipNote?> GetByIdIncludingDeletedAsync(int id)
    {
        return await _context.InternshipNotes.IgnoreQueryFilters().FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<(List<InternshipNote> Items, int TotalCount)> GetPagedByUserIdAsync(
        int userId, int page, int pageSize, NoteStatus? status, string? search)
    {
        var query = _context.InternshipNotes.Where(n => n.UserId == userId);
        query = WithStatus(query, status);
        query = WithSearch(query, search, includeAuthor: false);
        return await PageNewestFirstAsync(query, page, pageSize);
    }

    public async Task<(List<InternshipNote> Items, int TotalCount)> GetPagedForMentorAsync(
        int mentorId, int page, int pageSize, NoteStatus? status, string? search)
    {
        // Mentorun gruplarındaki stajyerler: Groups ve GroupMembers query filter'ları soft-delete edilmiş olanları zaten gizler.
        var mentorGroupIds = _context.Groups.Where(g => g.MentorId == mentorId).Select(g => g.Id);
        var internIds = _context.GroupMembers.Where(gm => mentorGroupIds.Contains(gm.GroupId)).Select(gm => gm.UserId);

        // Taslaklar stajyerin özelidir, mentora hiç gösterilmez.
        var query = _context.InternshipNotes.Where(n => internIds.Contains(n.UserId) && n.Status != NoteStatus.Draft);
        query = WithStatus(query, status);
        query = WithSearch(query, search, includeAuthor: true);
        return await PageNewestFirstAsync(query, page, pageSize);
    }

    public async Task<List<InternshipNote>> GetRangeByUserIdAsync(int userId, DateTime? from, DateTime? to, int max)
    {
        var query = _context.InternshipNotes.Where(n => n.UserId == userId);
        if (from.HasValue)
        {
            query = query.Where(n => n.NoteDate >= from.Value.Date);
        }

        if (to.HasValue)
        {
            query = query.Where(n => n.NoteDate < to.Value.Date.AddDays(1));
        }

        return await query.OrderBy(n => n.NoteDate).ThenBy(n => n.Id).Take(max).ToListAsync();
    }

    public async Task<bool> ExistsOnDayAsync(int userId, DateTime day, int? excludeNoteId)
    {
        var start = day.Date;
        var end = start.AddDays(1);
        return await _context.InternshipNotes.AnyAsync(n =>
            n.UserId == userId && n.NoteDate >= start && n.NoteDate < end && (excludeNoteId == null || n.Id != excludeNoteId));
    }

    public async Task AddAsync(InternshipNote note)
    {
        await _context.InternshipNotes.AddAsync(note);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    private static IQueryable<InternshipNote> WithStatus(IQueryable<InternshipNote> query, NoteStatus? status)
    {
        return status.HasValue ? query.Where(n => n.Status == status.Value) : query;
    }

    // Arama: başlık, yapılan iş, öğrenilenler, etiketler (mentor listesinde ek olarak stajyerin adı/e-postası).
    private IQueryable<InternshipNote> WithSearch(IQueryable<InternshipNote> query, string? search, bool includeAuthor)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        if (!includeAuthor)
        {
            return query.Where(n => n.Title.Contains(search) || n.Content.Contains(search)
                || n.Learned.Contains(search) || n.Tags.Contains(search));
        }

        var matchingUsers = _context.Users
            .Where(u => u.FullName.Contains(search) || u.Email.Contains(search))
            .Select(u => u.Id);

        return query.Where(n => n.Title.Contains(search) || n.Content.Contains(search)
            || n.Learned.Contains(search) || n.Tags.Contains(search) || matchingUsers.Contains(n.UserId));
    }

    // Defter, tarihe göre en yeni önce okunur (Id ile sıra sabit kalır).
    private static async Task<(List<InternshipNote> Items, int TotalCount)> PageNewestFirstAsync(
        IQueryable<InternshipNote> query, int page, int pageSize)
    {
        int totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.NoteDate)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
