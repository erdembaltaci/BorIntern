using Backend.Dtos;

namespace Backend.Services;

public interface ITaskService
{
    Task<TaskDto> CreateTaskAsync(int mentorId, CreateTaskRequestDto request);
    Task<TaskDto> UpdateTaskStatusAsync(int userId, int taskId, UpdateTaskStatusRequestDto request);
    Task<PagedResultDto<TaskDto>> GetMyTasksAsync(int userId, int page, int pageSize);

    // Tekil görev görüntüleme: sadece görevin sahibi (atanan stajyer) ya da onu oluşturan mentor.
    Task<TaskDto> GetTaskByIdAsync(int callerId, int taskId);

    // Sadece görevi OLUŞTURAN mentor silebilir/geri getirebilir.
    Task DeleteTaskAsync(int mentorId, int taskId);
    Task<TaskDto> RestoreTaskAsync(int mentorId, int taskId);

    // Mentor'un, kendi grubundaki bir stajyerin görev özetini (kaç tamamlanmış, kaç devam ediyor) görmesi.
    Task<TaskSummaryDto> GetPerformanceSummaryAsync(int mentorId, int userId);

    // Admin, tüm görevleri (hangi mentor/stajyer olursa olsun) görebilir.
    Task<PagedResultDto<TaskDto>> GetAllTasksAsync(int page, int pageSize);
}
