using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

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

    public async Task<List<TaskItem>> GetByAssignedUserIdAsync(int userId)
    {
        return await _context.Tasks.Where(t => t.AssignedUserId == userId).ToListAsync();
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
