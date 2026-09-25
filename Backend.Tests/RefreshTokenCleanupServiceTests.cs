using Backend.BackgroundServices;
using Backend.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Backend.Tests;

public class RefreshTokenCleanupServiceTests
{
    // BackgroundService, repository'yi bir "scope" içinden alıyor; testte o zinciri sahte nesnelerle kuruyoruz.
    private static RefreshTokenCleanupService CreateService(Mock<IRefreshTokenRepository> repository)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IRefreshTokenRepository))).Returns(repository.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new RefreshTokenCleanupService(scopeFactory.Object, NullLogger<RefreshTokenCleanupService>.Instance);
    }

    [Fact]
    public async Task CleanupAsync_SuresiDolmusTokenlariSildirirVeAdediDoner()
    {
        var mockRepo = new Mock<IRefreshTokenRepository>();
        mockRepo.Setup(r => r.DeleteExpiredAsync(It.IsAny<DateTime>())).ReturnsAsync(3);

        var service = CreateService(mockRepo);

        int deleted = await service.CleanupAsync();

        Assert.Equal(3, deleted);
        // "Şu an"a göre silmeli: gelecekteki ya da çok eski bir tarih verilmemiş olmalı.
        mockRepo.Verify(r => r.DeleteExpiredAsync(It.Is<DateTime>(d =>
            d <= DateTime.UtcNow && d > DateTime.UtcNow.AddMinutes(-1))), Times.Once);
    }

    [Fact]
    public async Task CleanupAsync_SilinecekTokenYoksa_SifirDoner()
    {
        var mockRepo = new Mock<IRefreshTokenRepository>();
        mockRepo.Setup(r => r.DeleteExpiredAsync(It.IsAny<DateTime>())).ReturnsAsync(0);

        var service = CreateService(mockRepo);

        Assert.Equal(0, await service.CleanupAsync());
    }
}
