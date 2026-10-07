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

        var service = new TaskService(mockTaskRepo.Object, mockGroupMemberRepo.Object, TestMocks.EmptyUsers());

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

        var service = new TaskService(mockTaskRepo.Object, mockGroupMemberRepo.Object, TestMocks.EmptyUsers());

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

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

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

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

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

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

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

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        var request = new UpdateTaskStatusRequestDto { Status = "Completed" };
        var result = await service.UpdateTaskStatusAsync(userId: 1, taskId: 1, request);

        Assert.Equal("Completed", result.Status);
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_KullaniciMentorunGrubundaDegilse_ForbiddenExceptionFirlatir()
    {
        var mockGroupMemberRepo = new Mock<IGroupMemberRepository>();
        mockGroupMemberRepo.Setup(r => r.IsUserInMentorGroupAsync(1, 5)).ReturnsAsync(false);

        var service = new TaskService(new Mock<ITaskRepository>().Object, mockGroupMemberRepo.Object, TestMocks.EmptyUsers());

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

        var service = new TaskService(mockTaskRepo.Object, mockGroupMemberRepo.Object, TestMocks.EmptyUsers());

        var result = await service.GetPerformanceSummaryAsync(mentorId: 1, userId: 5);

        Assert.Equal(4, result.TotalTasks);
        Assert.Equal(1, result.TodoCount);
        Assert.Equal(1, result.InProgressCount);
        Assert.Equal(2, result.CompletedCount);
    }

    [Fact]
    public async Task GetTaskByIdAsync_GorevBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((TaskItem?)null);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetTaskByIdAsync(callerId: 1, taskId: 99));
    }

    [Fact]
    public async Task GetTaskByIdAsync_NeAtananNeOlusturan_ForbiddenExceptionFirlatir()
    {
        var task = new TaskItem { Id = 1, Title = "Test", AssignedUserId = 5, CreatedByUserId = 2 };

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetTaskByIdAsync(callerId: 1, taskId: 1));
    }

    [Fact]
    public async Task GetTaskByIdAsync_AtananKullanici_GoreviGorebilir()
    {
        var task = new TaskItem { Id = 1, Title = "Test", AssignedUserId = 5, CreatedByUserId = 2 };

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        var result = await service.GetTaskByIdAsync(callerId: 5, taskId: 1);

        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task GetTaskByIdAsync_OlusturanMentor_GoreviGorebilir()
    {
        var task = new TaskItem { Id = 1, Title = "Test", AssignedUserId = 5, CreatedByUserId = 2 };

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        var result = await service.GetTaskByIdAsync(callerId: 2, taskId: 1);

        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task DeleteTaskAsync_GorevBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((TaskItem?)null);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteTaskAsync(mentorId: 1, taskId: 99));
    }

    [Fact]
    public async Task DeleteTaskAsync_OlusturanMentorDegilse_ForbiddenExceptionFirlatir()
    {
        var task = new TaskItem { Id = 1, Title = "Test", AssignedUserId = 5, CreatedByUserId = 2 };

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteTaskAsync(mentorId: 1, taskId: 1));
    }

    [Fact]
    public async Task DeleteTaskAsync_GecerliIstek_SoftDeleteYapilir()
    {
        var task = new TaskItem { Id = 1, Title = "Test", AssignedUserId = 5, CreatedByUserId = 2, IsDeleted = false };

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await service.DeleteTaskAsync(mentorId: 2, taskId: 1);

        Assert.True(task.IsDeleted);
        Assert.NotNull(task.DeletedAt);
    }

    [Fact]
    public async Task RestoreTaskAsync_GorevBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdIncludingDeletedAsync(It.IsAny<int>())).ReturnsAsync((TaskItem?)null);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await Assert.ThrowsAsync<NotFoundException>(() => service.RestoreTaskAsync(mentorId: 1, taskId: 99));
    }

    [Fact]
    public async Task RestoreTaskAsync_OlusturanMentorDegilse_ForbiddenExceptionFirlatir()
    {
        var task = new TaskItem
        {
            Id = 1, Title = "Test", AssignedUserId = 5, CreatedByUserId = 2,
            IsDeleted = true, DeletedAt = DateTime.UtcNow
        };

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdIncludingDeletedAsync(1)).ReturnsAsync(task);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.RestoreTaskAsync(mentorId: 1, taskId: 1));
    }

    [Fact]
    public async Task RestoreTaskAsync_ZatenSilinmemisse_InvalidOperationExceptionFirlatir()
    {
        var task = new TaskItem { Id = 1, Title = "Test", AssignedUserId = 5, CreatedByUserId = 2, IsDeleted = false };

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdIncludingDeletedAsync(1)).ReturnsAsync(task);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreTaskAsync(mentorId: 2, taskId: 1));
    }

    [Fact]
    public async Task RestoreTaskAsync_GecerliIstek_IsDeletedFalseOlur()
    {
        var task = new TaskItem
        {
            Id = 1, Title = "Test", AssignedUserId = 5, CreatedByUserId = 2,
            IsDeleted = true, DeletedAt = DateTime.UtcNow
        };

        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetByIdIncludingDeletedAsync(1)).ReturnsAsync(task);

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        var result = await service.RestoreTaskAsync(mentorId: 2, taskId: 1);

        Assert.False(task.IsDeleted);
        Assert.Null(task.DeletedAt);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task GetAllTasksAsync_RepodakiTumGorevleriDtoyaCevirir()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetPagedAllAsync(1, 20, null)).ReturnsAsync((new List<TaskItem>
        {
            new() { Id = 1, Title = "A", AssignedUserId = 5, CreatedByUserId = 1 },
            new() { Id = 2, Title = "B", AssignedUserId = 6, CreatedByUserId = 1 }
        }, 2));

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        var result = await service.GetAllTasksAsync(page: 1, pageSize: 20, search: null);

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetMyTasksAsync_SayfaBilgisiVeToplamKayitSayisiDoner()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetPagedByAssignedUserIdAsync(5, 2, 10, null)).ReturnsAsync((new List<TaskItem>
        {
            new() { Id = 11, Title = "A", AssignedUserId = 5, CreatedByUserId = 1 }
        }, 11));

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        var result = await service.GetMyTasksAsync(userId: 5, page: 2, pageSize: 10, status: null);

        Assert.Single(result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(11, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task GetCreatedTasksAsync_MentorunAtadigiGorevlerSayfalanarakDoner()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetPagedByCreatorIdAsync(1, 1, 20, null)).ReturnsAsync((new List<TaskItem>
        {
            new() { Id = 10, Title = "Görev A", CreatedByUserId = 1, AssignedUserId = 5 },
            new() { Id = 11, Title = "Görev B", CreatedByUserId = 1, AssignedUserId = 6 }
        }, 2));

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        var result = await service.GetCreatedTasksAsync(mentorId: 1, page: 0, pageSize: -3, search: null);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task GetCreatedTasksAsync_AramaMetniKirpilarakRepositoryeAktarilir()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetPagedByCreatorIdAsync(1, 1, 20, "api")).ReturnsAsync((new List<TaskItem>(), 0));

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await service.GetCreatedTasksAsync(mentorId: 1, page: 1, pageSize: 20, search: " api ");

        mockTaskRepo.Verify(r => r.GetPagedByCreatorIdAsync(1, 1, 20, "api"), Times.Once);
    }

    [Fact]
    public async Task GetAllTasksAsync_BosAramaFiltresizSayilir()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetPagedAllAsync(1, 20, null)).ReturnsAsync((new List<TaskItem>(), 0));

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await service.GetAllTasksAsync(page: 1, pageSize: 20, search: "");

        mockTaskRepo.Verify(r => r.GetPagedAllAsync(1, 20, null), Times.Once);
    }

    [Theory]
    [InlineData("Todo", Backend.Entities.TaskStatus.Todo)]
    [InlineData("InProgress", Backend.Entities.TaskStatus.InProgress)]
    [InlineData("completed", Backend.Entities.TaskStatus.Completed)]
    public async Task GetMyTasksAsync_DurumFiltresiRepositoryeAktarilir(string status, Backend.Entities.TaskStatus expected)
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetPagedByAssignedUserIdAsync(5, 1, 10, expected)).ReturnsAsync((new List<TaskItem>(), 0));

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await service.GetMyTasksAsync(userId: 5, page: 1, pageSize: 10, status: status);

        mockTaskRepo.Verify(r => r.GetPagedByAssignedUserIdAsync(5, 1, 10, expected), Times.Once);
    }

    [Theory]
    [InlineData("Bitmis")]
    [InlineData("99")]
    public async Task GetMyTasksAsync_GecersizDurum_InvalidOperationExceptionFirlatir(string status)
    {
        var service = new TaskService(new Mock<ITaskRepository>().Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetMyTasksAsync(userId: 5, page: 1, pageSize: 10, status: status));
    }

    [Fact]
    public async Task GetMySummaryAsync_SayilariVeGecikenleriRepodanAlirToplamiHesaplar()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetStatusCountsAsync(5, DateTime.UtcNow.Date)).ReturnsAsync((2, 3, 4, 1));

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        var result = await service.GetMySummaryAsync(userId: 5);

        Assert.Equal(9, result.TotalTasks);
        Assert.Equal(2, result.TodoCount);
        Assert.Equal(3, result.InProgressCount);
        Assert.Equal(4, result.CompletedCount);
        Assert.Equal(1, result.OverdueCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 5)]
    [InlineData(500, 20)]
    public async Task GetMyUpcomingTasksAsync_AdetSinirlandirilir(int requested, int expectedTake)
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetUpcomingByAssignedUserIdAsync(5, expectedTake)).ReturnsAsync(new List<TaskItem>());

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, TestMocks.EmptyUsers());

        await service.GetMyUpcomingTasksAsync(userId: 5, take: requested);

        mockTaskRepo.Verify(r => r.GetUpcomingByAssignedUserIdAsync(5, expectedTake), Times.Once);
    }

    [Fact]
    public async Task GetAllTasksAsync_AtananVeAtayanKisininAdiDoldurulur()
    {
        var mockTaskRepo = new Mock<ITaskRepository>();
        mockTaskRepo.Setup(r => r.GetPagedAllAsync(1, 20, null)).ReturnsAsync((new List<TaskItem>
        {
            new() { Id = 1, Title = "A", AssignedUserId = 5, CreatedByUserId = 9 },
            new() { Id = 2, Title = "B", AssignedUserId = 6, CreatedByUserId = 9 }
        }, 2));

        var mockUsers = new Mock<IUserRepository>();
        mockUsers.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<Backend.Entities.User>
        {
            new() { Id = 5, FullName = "Elif" },
            new() { Id = 9, FullName = "Zeynep" }
            // 6 numaralı kullanıcı bulunamıyor: liste yine de dönmeli, ad boş kalmalı.
        });

        var service = new TaskService(mockTaskRepo.Object, new Mock<IGroupMemberRepository>().Object, mockUsers.Object);

        var result = await service.GetAllTasksAsync(page: 1, pageSize: 20, search: null);

        Assert.Equal("Elif", result.Items[0].AssignedUserName);
        Assert.Equal("Zeynep", result.Items[0].CreatedByUserName);
        Assert.Equal(string.Empty, result.Items[1].AssignedUserName);
        // Tek toplu sorgu: görev başına ayrı kullanıcı sorgusu atılmamalı.
        mockUsers.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>()), Times.Once);
        mockUsers.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }
}
