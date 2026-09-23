using Backend.Dtos;
using Backend.Exceptions;
using Backend.Repositories;
using TaskItem = Backend.Entities.TaskItem;
// System.Threading.Tasks.TaskStatus ile bizim Backend.Entities.TaskStatus'umuz aynı isimde olduğu
// için, derleyicinin hangisini kastettiğimizi anlaması adına açıkça "takma isim" veriyoruz.
using TaskStatus = Backend.Entities.TaskStatus;

namespace Backend.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupMemberRepository _groupMemberRepository;

    public TaskService(ITaskRepository taskRepository, IGroupMemberRepository groupMemberRepository)
    {
        _taskRepository = taskRepository;
        _groupMemberRepository = groupMemberRepository;
    }

    public async Task<TaskDto> CreateTaskAsync(int mentorId, CreateTaskRequestDto request)
    {
        // Mentor sahiplik kontrolü: sadece kendi grubundaki bir stajyere görev atayabilir.
        bool isInMentorGroup = await _groupMemberRepository.IsUserInMentorGroupAsync(mentorId, request.AssignedUserId);
        if (!isInMentorGroup)
        {
            throw new ForbiddenException("Bu kullanıcı sizin grubunuzda değil.");
        }

        var task = new TaskItem
        {
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            AssignedUserId = request.AssignedUserId,
            CreatedByUserId = mentorId
        };

        await _taskRepository.AddAsync(task);
        await _taskRepository.SaveChangesAsync();

        return MapToDto(task);
    }

    public async Task<TaskDto> UpdateTaskStatusAsync(int userId, int taskId, UpdateTaskStatusRequestDto request)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        // Görev sahipliği kontrolü: sadece kendine atanmış görevi güncelleyebilirsin.
        if (task.AssignedUserId != userId)
        {
            throw new ForbiddenException("Bu görev size ait değil.");
        }

        if (!Enum.TryParse<TaskStatus>(request.Status, out var newStatus))
        {
            throw new InvalidOperationException("Geçersiz görev durumu.");
        }

        task.Status = newStatus;
        await _taskRepository.SaveChangesAsync();

        return MapToDto(task);
    }

    public async Task<List<TaskDto>> GetMyTasksAsync(int userId)
    {
        var tasks = await _taskRepository.GetByAssignedUserIdAsync(userId);
        return tasks.Select(MapToDto).ToList();
    }

    public async Task<TaskDto> GetTaskByIdAsync(int callerId, int taskId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        // Ya görevin sahibi (atanan stajyer) ya da onu oluşturan mentor görebilir.
        if (task.AssignedUserId != callerId && task.CreatedByUserId != callerId)
        {
            throw new ForbiddenException("Bu görevi görme yetkiniz yok.");
        }

        return MapToDto(task);
    }

    public async Task DeleteTaskAsync(int mentorId, int taskId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        if (task.CreatedByUserId != mentorId)
        {
            throw new ForbiddenException("Bu görevi silme yetkiniz yok.");
        }

        task.IsDeleted = true;
        task.DeletedAt = DateTime.UtcNow;
        await _taskRepository.SaveChangesAsync();
    }

    public async Task<TaskDto> RestoreTaskAsync(int mentorId, int taskId)
    {
        var task = await _taskRepository.GetByIdIncludingDeletedAsync(taskId);
        if (task == null)
        {
            throw new NotFoundException("Görev bulunamadı.");
        }

        if (task.CreatedByUserId != mentorId)
        {
            throw new ForbiddenException("Bu görevi geri getirme yetkiniz yok.");
        }

        if (!task.IsDeleted)
        {
            throw new InvalidOperationException("Bu görev zaten silinmemiş.");
        }

        task.IsDeleted = false;
        task.DeletedAt = null;
        await _taskRepository.SaveChangesAsync();

        return MapToDto(task);
    }

    public async Task<TaskSummaryDto> GetPerformanceSummaryAsync(int mentorId, int userId)
    {
        // Mentor sahiplik kontrolü: sadece kendi grubundaki bir stajyerin özetini görebilir.
        bool isInMentorGroup = await _groupMemberRepository.IsUserInMentorGroupAsync(mentorId, userId);
        if (!isInMentorGroup)
        {
            throw new ForbiddenException("Bu kullanıcı sizin grubunuzda değil.");
        }

        var tasks = await _taskRepository.GetByAssignedUserIdAsync(userId);

        return new TaskSummaryDto
        {
            TotalTasks = tasks.Count,
            TodoCount = tasks.Count(t => t.Status == TaskStatus.Todo),
            InProgressCount = tasks.Count(t => t.Status == TaskStatus.InProgress),
            CompletedCount = tasks.Count(t => t.Status == TaskStatus.Completed)
        };
    }

    public async Task<List<TaskDto>> GetAllTasksAsync()
    {
        var tasks = await _taskRepository.GetAllAsync();
        return tasks.Select(MapToDto).ToList();
    }

    private static TaskDto MapToDto(TaskItem task)
    {
        return new TaskDto
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status.ToString(),
            DueDate = task.DueDate,
            AssignedUserId = task.AssignedUserId,
            CreatedByUserId = task.CreatedByUserId,
            CreatedAt = task.CreatedAt
        };
    }
}
