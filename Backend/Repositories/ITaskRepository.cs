using Backend.Entities;

namespace Backend.Repositories;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(int id);

    // Silinmiş (IsDeleted=true) görevleri de görebilen versiyon - restore işlemi için.
    Task<TaskItem?> GetByIdIncludingDeletedAsync(int id);

    Task<List<TaskItem>> GetByAssignedUserIdAsync(int userId);

    // Admin'in tüm görevleri (hangi mentor/stajyer olursa olsun) görebilmesi için.
    Task<List<TaskItem>> GetAllAsync();
    Task AddAsync(TaskItem task);
    Task SaveChangesAsync();
}
