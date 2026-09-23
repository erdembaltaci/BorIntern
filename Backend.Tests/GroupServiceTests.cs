using Backend.Dtos;
using Backend.Entities;
using Backend.Repositories;
using Backend.Services;
using Moq;
using Xunit;

namespace Backend.Tests;

public class GroupServiceTests
{
    [Fact]
    public async Task CreateGroupAsync_AyniIsimdeGrupVarsa_InvalidOperationExceptionFirlatir()
    {
        var mockRepo = new Mock<IGroupRepository>();
        mockRepo.Setup(r => r.GroupNameExistsAsync("Backend Ekibi")).ReturnsAsync(true);

        var service = new GroupService(mockRepo.Object);

        var request = new CreateGroupRequestDto { Name = "Backend Ekibi" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateGroupAsync(mentorId: 1, request));
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
        mockRepo.Setup(r => r.GetByMentorIdAsync(1)).ReturnsAsync(new List<Group>
        {
            new() { Id = 1, Name = "Grup A", MentorId = 1 },
            new() { Id = 2, Name = "Grup B", MentorId = 1 }
        });

        var service = new GroupService(mockRepo.Object);

        var result = await service.GetMyGroupsAsync(1);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, g => g.Name == "Grup A");
    }
}
