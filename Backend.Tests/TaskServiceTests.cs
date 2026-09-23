using Backend.Dtos;
using Backend.Exceptions;
using Backend.Repositories;
using Backend.Services;
using Moq;
using Xunit;
using TaskItem = Backend.Entities.TaskItem;
// AuthService.cs'te de açıkladığımız gibi: System.Threading.Tasks.TaskStatus ile bizim
// Backend.Entities.TaskStatus'umuz aynı isimde olduğu için takma isim veriyoruz.
using TaskStatus = Backend.Entities.TaskStatus;

namespace Backend.Tests;

public class TaskServiceTests
{
    [Fact]
    public async Task CreateTaskAsync_KullaniciMentorunGrubundaDegilse_ForbiddenExceptionFirlatir()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        var mockGroupMemberRepo = new Mock<IGroupMemberRepository>();
        mockGroupMemberRepo.Setup(r => r.IsUserInMentorGroupAsync(1, 5)).ReturnsAsync(false);

        var service = new TaskService(mockTaskRepo.Object, mockGroupMemberRepo.Object);

        var request = new CreateTaskRequestDto { Title = "Test Görev", AssignedUserId = 5 };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateTaskAsync(mentorId: 1, request));
    }

    [Fact]
    public async Task CreateTaskAsync_GecerliIstek_CreatedByUserIdMentorOlur()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        var mockGroupMemberRepo = new Mock<IGroupMemberRepository>();
        mockGroupMemberRepo.Setup(r => r.IsUserInMentorGroupAsync(1, 5)).ReturnsAsync(true);

        TaskItem? capturedTask = null;
        mockTaskRepo.Setup(r => r.AddAsync(It.IsAny<TaskItem>()))
            .Callback<TaskItem>(t => capturedTask = t)
            .Returns(Task.CompletedTask);

        var service = new TaskService(mockTaskRepo.Object, mockGroupMemberRepo.Object);

        var request = new CreateTaskRequestDto { Title = "Test Görev", AssignedUserId = 5 };
        var result = await service.CreateTaskAsync(mentorId: 1, request);

        Assert.Equal(1, result.CreatedByUserId);
        Assert.Equal(5, result.AssignedUserId);
        Assert.Equal("Todo", result.Status); // entity'de varsayılan değer
        Assert.NotNull(capturedTask);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_GorevBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((TaskItem?)null);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object);

        var request = new UpdateTaskStatusRequestDto { Status = "InProgress" };

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.UpdateTaskStatusAsync(userId: 1, taskId: 99, request));
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_BaskasininGorevi_ForbiddenExceptionFirlatir()
    {
        var task = new TaskItem { Id = 1, Title = "Test", AssignedUserId = 5, CreatedByUserId = 2 };

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object);

        var request = new UpdateTaskStatusRequestDto { Status = "InProgress" };

        // userId=1 gönderiyoruz ama görev 5 numaralı kullanıcıya atanmış -> Forbidden beklenir.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.UpdateTaskStatusAsync(userId: 1, taskId: 1, request));
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_GecersizDurumMetni_InvalidOperationExceptionFirlatir()
    {
        var task = new TaskItem { Id = 1, Title = "Test", AssignedUserId = 1, CreatedByUserId = 2 };

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object);

        var request = new UpdateTaskStatusRequestDto { Status = "BoyleBirDurumYok" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateTaskStatusAsync(userId: 1, taskId: 1, request));
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_GecerliIstek_DurumGuncellenir()
    {
        var task = new TaskItem { Id = 1, Title = "Test", AssignedUserId = 1, CreatedByUserId = 2 };

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object);

        var request = new UpdateTaskStatusRequestDto { Status = "Completed" };
        var result = await service.UpdateTaskStatusAsync(userId: 1, taskId: 1, request);

        Assert.Equal("Completed", result.Status);
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_KullaniciMentorunGrubundaDegilse_ForbiddenExceptionFirlatir()
    {
        var mockGroupMemberRepo = new Mock<IGroupMemberRepository>();
        mockGroupMemberRepo.Setup(r => r.IsUserInMentorGroupAsync(1, 5)).ReturnsAsync(false);

        var service = new TaskService(new Mock<ITaskRepository>().Object, mockGroupMemberRepo.Object);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.GetPerformanceSummaryAsync(mentorId: 1, userId: 5));
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_GecerliIstek_DurumSayilariDogruHesaplanir()
    {
        var mockGroupMemberRepo = new Mock<IGroupMemberRepository>();
        mockGroupMemberRepo.Setup(r => r.IsUserInMentorGroupAsync(1, 5)).ReturnsAsync(true);

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByAssignedUserIdAsync(5)).ReturnsAsync(new List<TaskItem>
        {
            new() { Id = 1, Title = "A", AssignedUserId = 5, CreatedByUserId = 1, Status = TaskStatus.Todo },
            new() { Id = 2, Title = "B", AssignedUserId = 5, CreatedByUserId = 1, Status = TaskStatus.InProgress },
            new() { Id = 3, Title = "C", AssignedUserId = 5, CreatedByUserId = 1, Status = TaskStatus.Completed },
            new() { Id = 4, Title = "D", AssignedUserId = 5, CreatedByUserId = 1, Status = TaskStatus.Completed }
        });

        var service = new TaskService(mockTaskRepo.Object, mockGroupMemberRepo.Object);

        var result = await service.GetPerformanceSummaryAsync(mentorId: 1, userId: 5);

        Assert.Equal(4, result.TotalTasks);
        Assert.Equal(1, result.TodoCount);
        Assert.Equal(1, result.InProgressCount);
        Assert.Equal(2, result.CompletedCount);
    }
}
