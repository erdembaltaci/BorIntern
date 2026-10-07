using Backend.Entities;
// System.Threading.Tasks.TaskStatus ile çakışmaması için açık takma ad.
using TaskStatus = Backend.Entities.TaskStatus;

namespace Backend.Repositories;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(int id);

    // Silinmiş (IsDeleted=true) görevleri de görebilen versiyon - restore işlemi için.
    Task<TaskItem?> GetByIdIncludingDeletedAsync(int id);

    // Sayfalanmamış hali sadece performans özeti için (tüm görevleri saymak gerekiyor).
    Task<List<TaskItem>> GetByAssignedUserIdAsync(int userId);
    // status: sadece o durumdaki görevler (null = hepsi). Pano sütunları ve liste filtresi bunu kullanır.
    Task<(List<TaskItem> Items, int TotalCount)> GetPagedByAssignedUserIdAsync(int userId, int page, int pageSize, TaskStatus? status);

    // Durum başına görev sayısı + bugünden önce bitmesi gerekip tamamlanmamış (geciken) sayısı; tek gruplu sorgu.
    Task<(int Todo, int InProgress, int Completed, int Overdue)> GetStatusCountsAsync(int userId, DateTime today);

    // Bitmemiş görevler, bitiş tarihi en yakın olandan başlayarak (tarihsizler en sonda); en fazla `take` adet.
    Task<List<TaskItem>> GetUpcomingByAssignedUserIdAsync(int userId, int take);

    // Mentor'un kendi atadığı görevleri (hangi stajyere olursa olsun) listelemesi için.
    // search: başlık, açıklama veya stajyerin adı/e-postasında geçen metin (null = filtre yok).
    Task<(List<TaskItem> Items, int TotalCount)> GetPagedByCreatorIdAsync(int creatorId, int page, int pageSize, string? search);

    // Admin'in tüm görevleri (hangi mentor/stajyer olursa olsun) görebilmesi için.
    Task<(List<TaskItem> Items, int TotalCount)> GetPagedAllAsync(int page, int pageSize, string? search);
    Task AddAsync(TaskItem task);
    Task SaveChangesAsync();
}
