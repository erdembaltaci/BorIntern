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
        mockRepo.Setup(r => r.GetPagedByStatusAsync(UserStatus.Pending, 1, 20, null))
            .ReturnsAsync((new List<User> { new() { Id = 1, Email = "a@b.com", FullName = "Test" } }, 1));

        var service = new UserService(mockRepo.Object);

        var result = await service.GetPendingUsersAsync(page: 1, pageSize: 20, search: null);

        Assert.Single(result.Items);
        // GetPagedAsync'in DEĞİL, GetPagedByStatusAsync(Pending)'in çağrıldığını doğruluyoruz -
        // yani servis gerçekten "filtreli" sorguyu kullanmış, tüm kullanıcıları çekip kendi
        // elemesini yapmamış (performans açısından önemli bir fark).
        mockRepo.Verify(r => r.GetPagedByStatusAsync(UserStatus.Pending, 1, 20, null), Times.Once);
        mockRepo.Verify(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ChangeUserRoleAsync_KendiRolunuDegistirmeyeCalisirsa_InvalidOperationExceptionFirlatir()
    {
        var service = new UserService(new Mock<IUserRepository>().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeUserRoleAsync(adminId: 1, userId: 1, new UpdateUserRoleRequestDto { Role = "Mentor" }));
    }

    [Theory]
    [InlineData("BoyleBirRolYok")]
    [InlineData("99")]
    public async Task ChangeUserRoleAsync_GecersizRol_InvalidOperationExceptionFirlatir(string role)
    {
        var service = new UserService(new Mock<IUserRepository>().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeUserRoleAsync(adminId: 1, userId: 2, new UpdateUserRoleRequestDto { Role = role }));
    }

    [Fact]
    public async Task ChangeUserRoleAsync_KullaniciBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((User?)null);

        var service = new UserService(mockRepo.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.ChangeUserRoleAsync(adminId: 1, userId: 99, new UpdateUserRoleRequestDto { Role = "Mentor" }));
    }

    [Theory]
    [InlineData("Mentor")]
    [InlineData("mentor")]
    public async Task ChangeUserRoleAsync_GecerliRol_RolGuncellenirVeKaydedilir(string role)
    {
        var user = new User { Id = 2, Email = "a@b.com", FullName = "Test", Role = UserRole.Intern };

        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(user);

        var service = new UserService(mockRepo.Object);

        var result = await service.ChangeUserRoleAsync(adminId: 1, userId: 2, new UpdateUserRoleRequestDto { Role = role });

        Assert.Equal("Mentor", result.Role);
        Assert.Equal(UserRole.Mentor, user.Role);
        mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllUsersAsync_GecersizSayfaDegerleriVarsayilanaCekilir()
    {
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetPagedAsync(1, 100, null)).ReturnsAsync((new List<User>(), 0));

        var service = new UserService(mockRepo.Object);

        var result = await service.GetAllUsersAsync(page: -3, pageSize: 500, search: null);

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        mockRepo.Verify(r => r.GetPagedAsync(1, 100, null), Times.Once);
    }

    [Fact]
    public async Task SearchActiveInternsAsync_AramaMetniVeSayfalamaRepositoryeAktarilir()
    {
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetPagedActiveInternsAsync("ali", 1, 20)).ReturnsAsync((new List<User>
        {
            new() { Id = 3, FullName = "Ali Veli", Email = "ali@mail.com", Role = UserRole.Intern, Status = UserStatus.Active }
        }, 1));

        var service = new UserService(mockRepo.Object);

        // page=0 ve pageSize=0 geçersiz: Normalize ile 1 ve 20'ye çekilmeli.
        var result = await service.SearchActiveInternsAsync("ali", page: 0, pageSize: 0);

        Assert.Single(result.Items);
        Assert.Equal("Ali Veli", result.Items[0].FullName);
        Assert.Equal("Intern", result.Items[0].Role);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task GetAllUsersAsync_AramaMetniKirpilarakRepositoryeAktarilir()
    {
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetPagedAsync(1, 20, "ali")).ReturnsAsync((new List<User>(), 0));

        var service = new UserService(mockRepo.Object);

        await service.GetAllUsersAsync(page: 1, pageSize: 20, search: "  ali ");

        mockRepo.Verify(r => r.GetPagedAsync(1, 20, "ali"), Times.Once);
    }

    [Fact]
    public async Task GetPendingUsersAsync_BosAramaFiltresizSayilir()
    {
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetPagedByStatusAsync(UserStatus.Pending, 1, 20, null)).ReturnsAsync((new List<User>(), 0));

        var service = new UserService(mockRepo.Object);

        await service.GetPendingUsersAsync(page: 1, pageSize: 20, search: "   ");

        mockRepo.Verify(r => r.GetPagedByStatusAsync(UserStatus.Pending, 1, 20, null), Times.Once);
    }

    [Fact]
    public async Task SearchMyInternsAsync_MentorIdAramaVeSayfalamaRepositoryeAktarilir()
    {
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.GetPagedMentorInternsAsync(4, "elif", 1, 20)).ReturnsAsync((new List<User>
        {
            new() { Id = 7, FullName = "Elif Saraç", Email = "elif@mail.com", Role = UserRole.Intern, Status = UserStatus.Active }
        }, 1));

        var service = new UserService(mockRepo.Object);

        // page=0/pageSize=0 geçersiz (Normalize ile 1/20), arama metni kırpılır.
        var result = await service.SearchMyInternsAsync(mentorId: 4, search: "  elif ", page: 0, pageSize: 0);

        Assert.Single(result.Items);
        Assert.Equal("Elif Saraç", result.Items[0].FullName);
        mockRepo.Verify(r => r.GetPagedMentorInternsAsync(4, "elif", 1, 20), Times.Once);
    }
}
