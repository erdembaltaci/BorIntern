using Backend.Entities;

namespace Backend.Repositories;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(int id);

    // Silinmiş (IsDeleted=true) görevleri de görebilen versiyon - restore işlemi için.
    Task<TaskItem?> GetByIdIncludingDeletedAsync(int id);

    // Sayfalanmamış hali sadece performans özeti için (tüm görevleri saymak gerekiyor).
    Task<List<TaskItem>> GetByAssignedUserIdAsync(int userId);
    Task<(List<TaskItem> Items, int TotalCount)> GetPagedByAssignedUserIdAsync(int userId, int page, int pageSize);

    // Admin'in tüm görevleri (hangi mentor/stajyer olursa olsun) görebilmesi için.
    Task<(List<TaskItem> Items, int TotalCount)> GetPagedAllAsync(int page, int pageSize);
    Task AddAsync(TaskItem task);
    Task SaveChangesAsync();
}
