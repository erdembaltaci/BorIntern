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
            CreateFakeJwtConfig().Object, new Mock<ILoginAttemptTracker>().Object);

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
            CreateFakeJwtConfig().Object, new Mock<ILoginAttemptTracker>().Object);

        var request = new LoginRequestDto { Email = "test@example.com", Password = "Sifre123!" };

        var result = await authService.LoginAsync(request);

        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.Equal("Intern", result.User.Role);
    }

    [Fact]
    public async Task RegisterAsync_VarOlanEmail_ConflictExceptionFirlatir()
    {
        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.EmailExistsAsync("test@example.com")).ReturnsAsync(true);

        var authService = new AuthService(
            mockUserRepository.Object,
            new Mock<IRefreshTokenRepository>().Object,
            CreateFakeJwtConfig().Object, new Mock<ILoginAttemptTracker>().Object);

        var request = new RegisterRequestDto
        {
            FullName = "Test Kullanici",
            Email = "test@example.com",
            Password = "Sifre123!"
        };

        await Assert.ThrowsAsync<ConflictException>(() => authService.RegisterAsync(request));
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenBulunamazsa_UnauthorizedExceptionFirlatir()
    {
        var mockRefreshRepo = new Mock<IRefreshTokenRepository>();
        mockRefreshRepo.Setup(r => r.GetByTokenAsync(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

        var authService = new AuthService(
            new Mock<IUserRepository>().Object, mockRefreshRepo.Object, CreateFakeJwtConfig().Object, new Mock<ILoginAttemptTracker>().Object);

        var request = new RefreshTokenRequestDto { RefreshToken = "olmayan-token" };

        await Assert.ThrowsAsync<UnauthorizedException>(() => authService.RefreshTokenAsync(request));
    }

    [Fact]
    public async Task RefreshTokenAsync_IptalEdilmisTokenIse_UnauthorizedExceptionFirlatir()
    {
        var revokedToken = new RefreshToken
        {
            Token = "eski-token",
            UserId = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = true // daha önce kullanılmış/iptal edilmiş
        };

        var mockRefreshRepo = new Mock<IRefreshTokenRepository>();
        mockRefreshRepo.Setup(r => r.GetByTokenAsync(TokenHasher.Hash("eski-token"))).ReturnsAsync(revokedToken);

        var authService = new AuthService(
            new Mock<IUserRepository>().Object, mockRefreshRepo.Object, CreateFakeJwtConfig().Object, new Mock<ILoginAttemptTracker>().Object);

        var request = new RefreshTokenRequestDto { RefreshToken = "eski-token" };

        await Assert.ThrowsAsync<UnauthorizedException>(() => authService.RefreshTokenAsync(request));
    }

    [Fact]
    public async Task RefreshTokenAsync_SuresiDolmusTokenIse_UnauthorizedExceptionFirlatir()
    {
        var expiredToken = new RefreshToken
        {
            Token = "eski-token",
            UserId = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // geçmişte kalmış
            IsRevoked = false
        };

        var mockRefreshRepo = new Mock<IRefreshTokenRepository>();
        mockRefreshRepo.Setup(r => r.GetByTokenAsync(TokenHasher.Hash("eski-token"))).ReturnsAsync(expiredToken);

        var authService = new AuthService(
            new Mock<IUserRepository>().Object, mockRefreshRepo.Object, CreateFakeJwtConfig().Object, new Mock<ILoginAttemptTracker>().Object);

        var request = new RefreshTokenRequestDto { RefreshToken = "eski-token" };

        await Assert.ThrowsAsync<UnauthorizedException>(() => authService.RefreshTokenAsync(request));
    }

    [Fact]
    public async Task RefreshTokenAsync_GecerliToken_YeniTokenCiftiUreturVeEskisiniIptalEder()
    {
        var validToken = new RefreshToken
        {
            Token = "gecerli-token",
            UserId = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = false
        };
        var activeUser = new User { Id = 1, Email = "a@b.com", FullName = "Test", Status = UserStatus.Active };

        var mockRefreshRepo = new Mock<IRefreshTokenRepository>();
        mockRefreshRepo.Setup(r => r.GetByTokenAsync(TokenHasher.Hash("gecerli-token"))).ReturnsAsync(validToken);

        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(activeUser);

        var authService = new AuthService(mockUserRepo.Object, mockRefreshRepo.Object, CreateFakeJwtConfig().Object, new Mock<ILoginAttemptTracker>().Object);

        var request = new RefreshTokenRequestDto { RefreshToken = "gecerli-token" };
        var result = await authService.RefreshTokenAsync(request);

        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        // Rotation: eski token artık iptal edilmiş olmalı - tekrar kullanılamaz.
        Assert.True(validToken.IsRevoked);
    }

    [Fact]
    public async Task LogoutAsync_TokenBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockRefreshRepo = new Mock<IRefreshTokenRepository>();
        mockRefreshRepo.Setup(r => r.GetByTokenAsync(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

        var authService = new AuthService(
            new Mock<IUserRepository>().Object, mockRefreshRepo.Object, CreateFakeJwtConfig().Object, new Mock<ILoginAttemptTracker>().Object);

        var request = new RefreshTokenRequestDto { RefreshToken = "olmayan-token" };

        await Assert.ThrowsAsync<NotFoundException>(() => authService.LogoutAsync(request));
    }

    [Fact]
    public async Task LogoutAsync_GecerliToken_IsRevokedTrueOlur()
    {
        var token = new RefreshToken { Token = "gecerli-token", UserId = 1, IsRevoked = false };

        var mockRefreshRepo = new Mock<IRefreshTokenRepository>();
        mockRefreshRepo.Setup(r => r.GetByTokenAsync(TokenHasher.Hash("gecerli-token"))).ReturnsAsync(token);

        var authService = new AuthService(
            new Mock<IUserRepository>().Object, mockRefreshRepo.Object, CreateFakeJwtConfig().Object, new Mock<ILoginAttemptTracker>().Object);

        await authService.LogoutAsync(new RefreshTokenRequestDto { RefreshToken = "gecerli-token" });

        Assert.True(token.IsRevoked);
    }

    private static User CreateActiveUser(string password)
    {
        var user = new User
        {
            Id = 1, Email = "test@example.com", FullName = "Test Kullanici",
            Role = UserRole.Intern, Status = UserStatus.Active
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        return user;
    }

    [Fact]
    public async Task LoginAsync_HesapKilitliyse_TooManyRequestsExceptionFirlatirVeVeritabaniniSorgulamaz()
    {
        var mockUserRepo = new Mock<IUserRepository>();
        var mockTracker = new Mock<ILoginAttemptTracker>();
        mockTracker.Setup(t => t.IsLockedOut("test@example.com")).Returns(true);

        var authService = new AuthService(
            mockUserRepo.Object, new Mock<IRefreshTokenRepository>().Object, CreateFakeJwtConfig().Object, mockTracker.Object);

        // Doğru şifre girilse bile kilitli hesap reddedilmeli.
        var request = new LoginRequestDto { Email = "test@example.com", Password = "Sifre123!" };

        await Assert.ThrowsAsync<TooManyRequestsException>(() => authService.LoginAsync(request));
        mockUserRepo.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_YanlisSifre_HataSayacinaKaydedilir()
    {
        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo.Setup(r => r.GetByEmailAsync("test@example.com")).ReturnsAsync(CreateActiveUser("Sifre123!"));
        var mockTracker = new Mock<ILoginAttemptTracker>();

        var authService = new AuthService(
            mockUserRepo.Object, new Mock<IRefreshTokenRepository>().Object, CreateFakeJwtConfig().Object, mockTracker.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() => authService.LoginAsync(
            new LoginRequestDto { Email = "test@example.com", Password = "YanlisSifre1" }));

        mockTracker.Verify(t => t.RecordFailure("test@example.com"), Times.Once);
        mockTracker.Verify(t => t.Reset(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_KullaniciYoksa_HataSayacinaKaydedilir()
    {
        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        var mockTracker = new Mock<ILoginAttemptTracker>();

        var authService = new AuthService(
            mockUserRepo.Object, new Mock<IRefreshTokenRepository>().Object, CreateFakeJwtConfig().Object, mockTracker.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() => authService.LoginAsync(
            new LoginRequestDto { Email = "yok@example.com", Password = "Sifre123!" }));

        // Var olmayan e-postalar da sayılır; yoksa kilit davranışı hesabın varlığını ele verirdi.
        mockTracker.Verify(t => t.RecordFailure("yok@example.com"), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_BasariliGiris_HataSayacinaSifirlar()
    {
        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo.Setup(r => r.GetByEmailAsync("test@example.com")).ReturnsAsync(CreateActiveUser("Sifre123!"));
        var mockTracker = new Mock<ILoginAttemptTracker>();

        var authService = new AuthService(
            mockUserRepo.Object, new Mock<IRefreshTokenRepository>().Object, CreateFakeJwtConfig().Object, mockTracker.Object);

        await authService.LoginAsync(new LoginRequestDto { Email = "test@example.com", Password = "Sifre123!" });

        mockTracker.Verify(t => t.Reset("test@example.com"), Times.Once);
        mockTracker.Verify(t => t.RecordFailure(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_RefreshTokenVeritabaninaHashlenmisYazilir()
    {
        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo.Setup(r => r.GetByEmailAsync("test@example.com")).ReturnsAsync(CreateActiveUser("Sifre123!"));

        RefreshToken? stored = null;
        var mockRefreshRepo = new Mock<IRefreshTokenRepository>();
        mockRefreshRepo.Setup(r => r.AddAsync(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => stored = t)
            .Returns(Task.CompletedTask);

        var authService = new AuthService(
            mockUserRepo.Object, mockRefreshRepo.Object, CreateFakeJwtConfig().Object, new Mock<ILoginAttemptTracker>().Object);

        var result = await authService.LoginAsync(new LoginRequestDto { Email = "test@example.com", Password = "Sifre123!" });

        // İstemciye ham token gider, veritabanına sadece özeti yazılır: DB sızsa bile token kullanılamaz.
        Assert.NotNull(stored);
        Assert.NotEqual(result.RefreshToken, stored!.Token);
        Assert.Equal(TokenHasher.Hash(result.RefreshToken), stored.Token);
    }
}