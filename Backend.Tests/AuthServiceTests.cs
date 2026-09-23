using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;
using Backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Backend.Tests;

// Örnek/öğretici testler: AuthService'in gerçek veritabanına HİÇ dokunmadan test edilmesi.
// IUserRepository/IRefreshTokenRepository "sahte" (Mock) verildiği için, bu testler
// saniyeler içinde, Docker/SQL Server açık olmadan bile çalışır.
public class AuthServiceTests
{
    // Her testte tekrar etmesin diye: gerçekçi bir sahte IConfiguration hazırlayan yardımcı metod.
    private static Mock<IConfiguration> CreateFakeJwtConfig()
    {
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Jwt:Key"]).Returns("TestSuperGizliAnahtarEnAz32KarakterOlmaliZorunlu!");
        mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
        mockConfig.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
        return mockConfig;
    }

    [Fact]
    public async Task LoginAsync_PendingKullanici_UnauthorizedExceptionFirlatir()
    {
        // Arrange: "Pending" durumda, gerçek bir hash'lenmiş parolaya sahip sahte bir kullanıcı hazırla.
        var pendingUser = new User
        {
            Id = 1,
            Email = "test@example.com",
            FullName = "Test Kullanici",
            Role = UserRole.Intern,
            Status = UserStatus.Pending
        };
        pendingUser.PasswordHash = new PasswordHasher<User>().HashPassword(pendingUser, "Sifre123!");

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByEmailAsync("test@example.com")).ReturnsAsync(pendingUser);

        var authService = new AuthService(
            mockUserRepository.Object,
            new Mock<IRefreshTokenRepository>().Object,
            CreateFakeJwtConfig().Object);

        var request = new LoginRequestDto { Email = "test@example.com", Password = "Sifre123!" };

        // Act + Assert: parola doğru olsa bile, Status Pending olduğu için UnauthorizedException beklenir.
        await Assert.ThrowsAsync<UnauthorizedException>(() => authService.LoginAsync(request));
    }

    [Fact]
    public async Task LoginAsync_AktifKullaniciDogruSifre_TokenIcerenSonucDoner()
    {
        var activeUser = new User
        {
            Id = 1,
            Email = "test@example.com",
            FullName = "Test Kullanici",
            Role = UserRole.Intern,
            Status = UserStatus.Active
        };
        activeUser.PasswordHash = new PasswordHasher<User>().HashPassword(activeUser, "Sifre123!");

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByEmailAsync("test@example.com")).ReturnsAsync(activeUser);

        var authService = new AuthService(
            mockUserRepository.Object,
            new Mock<IRefreshTokenRepository>().Object,
            CreateFakeJwtConfig().Object);

        var request = new LoginRequestDto { Email = "test@example.com", Password = "Sifre123!" };

        var result = await authService.LoginAsync(request);

        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.Equal("Intern", result.User.Role);
    }

    [Fact]
    public async Task RegisterAsync_VarOlanEmail_InvalidOperationExceptionFirlatir()
    {
        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.EmailExistsAsync("test@example.com")).ReturnsAsync(true);

        var authService = new AuthService(
            mockUserRepository.Object,
            new Mock<IRefreshTokenRepository>().Object,
            CreateFakeJwtConfig().Object);

        var request = new RegisterRequestDto
        {
            FullName = "Test Kullanici",
            Email = "test@example.com",
            Password = "Sifre123!"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => authService.RegisterAsync(request));
    }
}
