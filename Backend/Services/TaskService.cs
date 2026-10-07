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
    private readonly IUserRepository _userRepository;

    public TaskService(
        ITaskRepository taskRepository,
        IGroupMemberRepository groupMemberRepository,
        IUserRepository userRepository)
    {
        _taskRepository = taskRepository;
        _groupMemberRepository = groupMemberRepository;
        _userRepository = userRepository;
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

        return await MapOneAsync(task);
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

        return await MapOneAsync(task);
    }

    // status: "Todo" / "InProgress" / "Completed" ya da boş (hepsi). Pano görünümü her sütun için ayrı sayfa ister.
    public async Task<PagedResultDto<TaskDto>> GetMyTasksAsync(int userId, int page, int pageSize, string? status)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (tasks, totalCount) = await _taskRepository.GetPagedByAssignedUserIdAsync(userId, page, pageSize, ParseStatusFilter(status));
        return PagedResultDto<TaskDto>.Create(await MapAllAsync(tasks), page, pageSize, totalCount);
    }

    // Durum sayıları + geciken görev sayısı, tek gruplu sorguyla (görevleri belleğe çekmeden).
    public async Task<TaskSummaryDto> GetMySummaryAsync(int userId)
    {
        // Bitiş günü "gün" bilgisi taşıdığı için kıyaslama bugünün başlangıcına göre yapılır.
        var counts = await _taskRepository.GetStatusCountsAsync(userId, DateTime.UtcNow.Date);

        return new TaskSummaryDto
        {
            TotalTasks = counts.Todo + counts.InProgress + counts.Completed,
            TodoCount = counts.Todo,
            InProgressCount = counts.InProgress,
            CompletedCount = counts.Completed,
            OverdueCount = counts.Overdue
        };
    }

    // Panelin "yaklaşan görevler" listesi: bitmemiş görevler, bitiş tarihi en yakın olandan (tarihsizler sonda).
    public async Task<List<TaskDto>> GetMyUpcomingTasksAsync(int userId, int take)
    {
        take = Math.Clamp(take, 1, 20);
        var tasks = await _taskRepository.GetUpcomingByAssignedUserIdAsync(userId, take);
        return await MapAllAsync(tasks);
    }

    public async Task<PagedResultDto<TaskDto>> GetCreatedTasksAsync(int mentorId, int page, int pageSize, string? search)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (tasks, totalCount) = await _taskRepository.GetPagedByCreatorIdAsync(mentorId, page, pageSize, Pagination.NormalizeSearch(search));
        return PagedResultDto<TaskDto>.Create(await MapAllAsync(tasks), page, pageSize, totalCount);
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

        return await MapOneAsync(task);
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

        return await MapOneAsync(task);
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

    public async Task<PagedResultDto<TaskDto>> GetAllTasksAsync(int page, int pageSize, string? search)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (tasks, totalCount) = await _taskRepository.GetPagedAllAsync(page, pageSize, Pagination.NormalizeSearch(search));
        return PagedResultDto<TaskDto>.Create(await MapAllAsync(tasks), page, pageSize, totalCount);
    }

    // Boş değer "filtre yok"; geçersiz bir metin sessizce yok sayılmaz, istemciye hata olarak döner.
    private static TaskStatus? ParseStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.TryParse<TaskStatus>(status.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new InvalidOperationException("Geçersiz görev durumu.");
        }

        return parsed;
    }

    // Atanan ve atayan kişilerin adları, görev başına ayrı sorgu atmamak için tek seferde (toplu) getirilir.
    private async Task<List<TaskDto>> MapAllAsync(List<TaskItem> tasks)
    {
        var userIds = tasks.SelectMany(t => new[] { t.AssignedUserId, t.CreatedByUserId });
        var names = (await _userRepository.GetByIdsAsync(userIds)).ToDictionary(u => u.Id, u => u.FullName);
        return tasks.Select(t => MapToDto(t, names)).ToList();
    }

    private async Task<TaskDto> MapOneAsync(TaskItem task)
    {
        return (await MapAllAsync(new List<TaskItem> { task }))[0];
    }

    private static TaskDto MapToDto(TaskItem task, Dictionary<int, string> names)
    {
        return new TaskDto
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status.ToString(),
            DueDate = task.DueDate,
            AssignedUserId = task.AssignedUserId,
            AssignedUserName = names.GetValueOrDefault(task.AssignedUserId, string.Empty),
            CreatedByUserId = task.CreatedByUserId,
            CreatedByUserName = names.GetValueOrDefault(task.CreatedByUserId, string.Empty),
            CreatedAt = task.CreatedAt
        };
    }
}
