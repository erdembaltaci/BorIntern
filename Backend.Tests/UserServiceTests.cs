using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;
using Backend.Services;
using Moq;
using Xunit;

namespace Backend.Tests;

public class UserServiceTests
{
    [Fact]
    public async Task ApproveUserAsync_KullaniciBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((User?)null);

        var service = new UserService(mockRepo.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => service.ApproveUserAsync(1));
    }

    [Fact]
    public async Task ApproveUserAsync_GecerliKullanici_StatusActiveOlur()
    {
        var pendingUser = new User { Id = 1, Email = "a@b.com", FullName = "Test", Status = UserStatus.Pending };

        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(pendingUser);

        var service = new UserService(mockRepo.Object);

        var result = await service.ApproveUserAsync(1);

        Assert.Equal("Active", result.Status);
        // SaveChangesAsync'in gerçekten çağrıldığını da doğruluyoruz - sadece nesneyi
        // değiştirip kaydetmeyi unutmak, sık yapılan bir hata olduğu için önemli bir kontrol.
        mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeactivateUserAsync_GecerliKullanici_StatusInactiveOlur()
    {
        var activeUser = new User { Id = 1, Email = "a@b.com", FullName = "Test", Status = UserStatus.Active };

        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(activeUser);

        var service = new UserService(mockRepo.Object);

        var result = await service.DeactivateUserAsync(1);

        Assert.Equal("Inactive", result.Status);
    }

    [Fact]
    public async Task DeactivateUserAsync_KullaniciBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((User?)null);

        var service = new UserService(mockRepo.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeactivateUserAsync(1));
    }

    [Fact]
    public async Task UpdateProfileAsync_GecerliKullanici_FullNameGuncellenir()
    {
        var user = new User { Id = 1, Email = "a@b.com", FullName = "Eski Isim", Status = UserStatus.Active };

        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        var service = new UserService(mockRepo.Object);

        var result = await service.UpdateProfileAsync(1, new UpdateProfileRequestDto { FullName = "Yeni Isim" });

        Assert.Equal("Yeni Isim", result.FullName);
    }

    [Fact]
    public async Task GetPendingUsersAsync_SadecePendingDurumundakileriIster()
    {
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByStatusAsync(UserStatus.Pending))
            .ReturnsAsync(new List<User> { new() { Id = 1, Email = "a@b.com", FullName = "Test" } });

        var service = new UserService(mockRepo.Object);

        var result = await service.GetPendingUsersAsync();

        Assert.Single(result);
        // GetAllAsync'in DEĞİL, GetByStatusAsync(Pending)'in çağrıldığını doğruluyoruz -
        // yani servis gerçekten "filtreli" sorguyu kullanmış, tüm kullanıcıları çekip kendi
        // elemesini yapmamış (performans açısından önemli bir fark).
        mockRepo.Verify(r => r.GetByStatusAsync(UserStatus.Pending), Times.Once);
        mockRepo.Verify(r => r.GetAllAsync(), Times.Never);
    }
}
