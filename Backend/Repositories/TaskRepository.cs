using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;
// System.Threading.Tasks.TaskStatus ile çakışmaması için açık takma ad.
using TaskStatus = Backend.Entities.TaskStatus;

namespace Backend.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;

    public TaskRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TaskItem?> GetByIdAsync(int id)
    {
        return await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<TaskItem?> GetByIdIncludingDeletedAsync(int id)
    {
        return await _context.Tasks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<TaskItem>> GetByAssignedUserIdAsync(int userId)
    {
        return await _context.Tasks.Where(t => t.AssignedUserId == userId).ToListAsync();
    }

    public async Task<(List<TaskItem> Items, int TotalCount)> GetPagedByAssignedUserIdAsync(int userId, int page, int pageSize, TaskStatus? status)
    {
        var query = _context.Tasks.Where(t => t.AssignedUserId == userId);
        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return await query.ToPagedAsync(page, pageSize);
    }

    public async Task<(int Todo, int InProgress, int Completed, int Overdue)> GetStatusCountsAsync(int userId, DateTime today)
    {
        var mine = _context.Tasks.Where(t => t.AssignedUserId == userId);

        // GROUP BY Status: veritabanı sayıları hesaplar, görevlerin kendisi belleğe gelmez.
        var grouped = await mine
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int CountOf(TaskStatus s) => grouped.FirstOrDefault(g => g.Status == s)?.Count ?? 0;

        int overdue = await mine.CountAsync(t => t.Status != TaskStatus.Completed && t.DueDate != null && t.DueDate < today);

        return (CountOf(TaskStatus.Todo), CountOf(TaskStatus.InProgress), CountOf(TaskStatus.Completed), overdue);
    }

    public async Task<List<TaskItem>> GetUpcomingByAssignedUserIdAsync(int userId, int take)
    {
        return await _context.Tasks
            .Where(t => t.AssignedUserId == userId && t.Status != TaskStatus.Completed)
            .OrderBy(t => t.DueDate == null)   // tarihsizler en sona
            .ThenBy(t => t.DueDate)
            .ThenBy(t => t.Id)
            .Take(take)
            .ToListAsync();
    }

    public async Task<(List<TaskItem> Items, int TotalCount)> GetPagedByCreatorIdAsync(int creatorId, int page, int pageSize, string? search)
    {
        return await WithSearch(_context.Tasks.Where(t => t.CreatedByUserId == creatorId), search, includeCreatorName: false)
            .ToPagedAsync(page, pageSize);
    }

    public async Task<(List<TaskItem> Items, int TotalCount)> GetPagedAllAsync(int page, int pageSize, string? search)
    {
        return await WithSearch(_context.Tasks, search, includeCreatorName: true).ToPagedAsync(page, pageSize);
    }

    // Arama metni varsa: başlıkta, açıklamada ya da ilgili kişinin adında/e-postasında geçenleri bırakır.
    // Kişi eşleşmesi alt sorguyla yapılır (görev kaydında sadece kullanıcı Id'si var).
    private IQueryable<TaskItem> WithSearch(IQueryable<TaskItem> query, string? search, bool includeCreatorName)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var matchingUsers = _context.Users
            .Where(u => u.FullName.Contains(search) || u.Email.Contains(search))
            .Select(u => u.Id);

        return includeCreatorName
            ? query.Where(t => t.Title.Contains(search) || t.Description.Contains(search)
                || matchingUsers.Contains(t.AssignedUserId) || matchingUsers.Contains(t.CreatedByUserId))
            : query.Where(t => t.Title.Contains(search) || t.Description.Contains(search)
                || matchingUsers.Contains(t.AssignedUserId));
    }

    public async Task AddAsync(TaskItem task)
    {
        await _context.Tasks.AddAsync(task);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
