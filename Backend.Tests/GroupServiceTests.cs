using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;
using Backend.Services;
using Moq;
using Xunit;

namespace Backend.Tests;

public class GroupServiceTests
{
    [Fact]
    public async Task CreateGroupAsync_AyniIsimdeGrupVarsa_ConflictExceptionFirlatir()
    {
        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GroupNameExistsAsync("Backend Ekibi")).ReturnsAsync(true);

        var service = new GroupService(mockRepo.Object);

        var request = new CreateGroupRequestDto { Name = "Backend Ekibi" };

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateGroupAsync(mentorId: 1, request));
    }

    [Fact]
    public async Task CreateGroupAsync_GecerliIstek_MentorIdIstektenDegilParametredenAtanir()
    {
        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GroupNameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        Group? capturedGroup = null;
        mockRepo.Setup(r => r.AddAsync(It.IsAny<Group>()))
            .Callback<Group>(g => capturedGroup = g)
            .Returns(Task.CompletedTask);

        var service = new GroupService(mockRepo.Object);

        var request = new CreateGroupRequestDto { Name = "Backend Ekibi" };
        var result = await service.CreateGroupAsync(mentorId: 42, request);

        // Metoda parametre olarak verdiğimiz mentorId (42), oluşturulan Group'a doğru atanmış mı -
        // "mentorId asla request body'sinden değil, her zaman token'dan gelir" kuralının testi.
        Assert.Equal(42, result.MentorId);
        Assert.NotNull(capturedGroup);
        Assert.Equal(42, capturedGroup!.MentorId);
    }

    [Fact]
    public async Task GetMyGroupsAsync_RepodanGelenListeyiDtoyaCevirir()
    {
        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetPagedByMentorIdAsync(1, 1, 20)).ReturnsAsync((new List<Group>
        {
            new() { Id = 1, Name = "Grup A", MentorId = 1 },
            new() { Id = 2, Name = "Grup B", MentorId = 1 }
        }, 2));

        var service = new GroupService(mockRepo.Object);

        var result = await service.GetMyGroupsAsync(1, page: 1, pageSize: 20);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, g => g.Name == "Grup A");
    }

    [Fact]
    public async Task GetGroupByIdAsync_GrupBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Group?)null);

        var service = new GroupService(mockRepo.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetGroupByIdAsync(mentorId: 1, groupId: 99));
    }

    [Fact]
    public async Task GetGroupByIdAsync_BaskaMentorunGrubu_ForbiddenExceptionFirlatir()
    {
        var group = new Group { Id = 1, Name = "Grup A", MentorId = 99 };

        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var service = new GroupService(mockRepo.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetGroupByIdAsync(mentorId: 1, groupId: 1));
    }

    [Fact]
    public async Task UpdateGroupNameAsync_GecerliIstek_IsimGuncellenir()
    {
        var group = new Group { Id = 1, Name = "Eski Isim", MentorId = 1 };

        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);
        mockRepo.Setup(r => r.GroupNameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var service = new GroupService(mockRepo.Object);

        var result = await service.UpdateGroupNameAsync(1, 1, new CreateGroupRequestDto { Name = "Yeni Isim" });

        Assert.Equal("Yeni Isim", result.Name);
    }

    [Fact]
    public async Task UpdateGroupNameAsync_BaskaMentorunGrubu_ForbiddenExceptionFirlatir()
    {
        var group = new Group { Id = 1, Name = "Eski Isim", MentorId = 99 };

        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var service = new GroupService(mockRepo.Object);

        var request = new CreateGroupRequestDto { Name = "Yeni Isim" };
        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateGroupNameAsync(1, 1, request));
    }

    [Fact]
    public async Task DeleteGroupAsync_GecerliIstek_SoftDeleteYapilir()
    {
        var group = new Group { Id = 1, Name = "Grup A", MentorId = 1, IsDeleted = false };

        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var service = new GroupService(mockRepo.Object);

        await service.DeleteGroupAsync(mentorId: 1, groupId: 1);

        Assert.True(group.IsDeleted);
        Assert.NotNull(group.DeletedAt);
    }

    [Fact]
    public async Task DeleteGroupAsync_BaskaMentorunGrubu_ForbiddenExceptionFirlatir()
    {
        var group = new Group { Id = 1, Name = "Grup A", MentorId = 99 };

        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var service = new GroupService(mockRepo.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteGroupAsync(mentorId: 1, groupId: 1));
    }

    [Fact]
    public async Task RestoreGroupAsync_ZatenSilinmemisse_InvalidOperationExceptionFirlatir()
    {
        var group = new Group { Id = 1, Name = "Grup A", MentorId = 1, IsDeleted = false };

        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetByIdIncludingDeletedAsync(1)).ReturnsAsync(group);

        var service = new GroupService(mockRepo.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreGroupAsync(1, 1));
    }

    [Fact]
    public async Task RestoreGroupAsync_GecerliIstek_IsDeletedFalseOlur()
    {
        var group = new Group
        {
            Id = 1, Name = "Grup A", MentorId = 1, IsDeleted = true, DeletedAt = DateTime.UtcNow
        };

        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetByIdIncludingDeletedAsync(1)).ReturnsAsync(group);

        var service = new GroupService(mockRepo.Object);

        var result = await service.RestoreGroupAsync(1, 1);

        Assert.False(group.IsDeleted);
        Assert.Null(group.DeletedAt);
        Assert.Equal("Grup A", result.Name);
    }

    [Fact]
    public async Task GetAllGroupsAsync_TumGruplariMentorFiltresiOlmadanDoner()
    {
        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetPagedAllAsync(1, 20)).ReturnsAsync((new List<Group>
        {
            new() { Id = 1, Name = "Grup A", MentorId = 1 },
            new() { Id = 2, Name = "Grup B", MentorId = 2 }
        }, 2));

        var service = new GroupService(mockRepo.Object);

        var result = await service.GetAllGroupsAsync(page: 1, pageSize: 20);

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task UpdateGroupNameAsync_AyniIsimBaskaGrupta_ConflictExceptionFirlatir()
    {
        var group = new Group { Id = 1, Name = "Eski Isim", MentorId = 1 };

        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);
        mockRepo.Setup(r => r.GroupNameExistsAsync("Baska Grubun Adi")).ReturnsAsync(true);

        var service = new GroupService(mockRepo.Object);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.UpdateGroupNameAsync(1, 1, new CreateGroupRequestDto { Name = "Baska Grubun Adi" }));
    }

    [Fact]
    public async Task UpdateGroupNameAsync_GrupAdiniAynenKorursa_HataVermez()
    {
        var group = new Group { Id = 1, Name = "Ayni Isim", MentorId = 1 };

        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);
        mockRepo.Setup(r => r.GroupNameExistsAsync("Ayni Isim")).ReturnsAsync(true);

        var service = new GroupService(mockRepo.Object);

        var result = await service.UpdateGroupNameAsync(1, 1, new CreateGroupRequestDto { Name = "Ayni Isim" });

        Assert.Equal("Ayni Isim", result.Name);
    }
}
