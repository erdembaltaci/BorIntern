using Backend.Entities;

namespace Backend.Repositories;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(int id);
    Task<List<TaskItem>> GetByAssignedUserIdAsync(int userId);
    Task AddAsync(TaskItem task);
    Task SaveChangesAsync();
}
