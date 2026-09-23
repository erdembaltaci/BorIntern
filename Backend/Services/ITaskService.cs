using Backend.Dtos;

namespace Backend.Services;

public interface ITaskService
{
    Task<TaskDto> CreateTaskAsync(int mentorId, CreateTaskRequestDto request);
    Task<TaskDto> UpdateTaskStatusAsync(int userId, int taskId, UpdateTaskStatusRequestDto request);
    Task<List<TaskDto>> GetMyTasksAsync(int userId);

    // Mentor'un, kendi grubundaki bir stajyerin görev özetini (kaç tamamlanmış, kaç devam ediyor) görmesi.
    Task<TaskSummaryDto> GetPerformanceSummaryAsync(int mentorId, int userId);
}
