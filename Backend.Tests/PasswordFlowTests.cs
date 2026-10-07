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

// Parola değiştirme ve "şifremi unuttum" akışları: gerçek veritabanı/e-posta olmadan, kurallar ve güvenlik özellikleri.
public class PasswordFlowTests
{
    private const string OldPassword = "EskiParola1";
    private const string NewPassword = "YeniParola2";

    private sealed class Fixture
    {
        public Mock<IUserRepository> Users { get; } = new();
        public Mock<IRefreshTokenRepository> RefreshTokens { get; } = new();
        public Mock<ILoginAttemptTracker> Tracker { get; } = new();
        public Mock<IPasswordResetTokenRepository> ResetTokens { get; } = new();
        public Mock<IEmailSender> Email { get; } = new();
        public AuthService Service { get; }

        public Fixture()
        {
            Email.SetupGet(e => e.IsConfigured).Returns(true); // gerçek e-posta tanımlıymış gibi (kapalı hâl ayrı testlerde)
            var config = new Mock<IConfiguration>();
            config.Setup(c => c["Jwt:Key"]).Returns("TestSuperGizliAnahtarEnAz32KarakterOlmaliZorunlu!");
            config.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
            config.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
            config.Setup(c => c["App:FrontendUrl"]).Returns("https://pusula.example.com/");

            Service = new AuthService(Users.Object, RefreshTokens.Object, config.Object, Tracker.Object, ResetTokens.Object, Email.Object);
        }

        public User ActiveUser(UserStatus status = UserStatus.Active)
        {
            var user = new User { Id = 1, Email = "elif@mail.com", FullName = "Elif Saraç", Role = UserRole.Intern, Status = status };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, OldPassword);
            Users.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            Users.Setup(r => r.GetByEmailAsync("elif@mail.com")).ReturnsAsync(user);
            return user;
        }

        public static bool PasswordMatches(User user, string password) =>
            new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
    }

    // ================================================================ parola değiştirme

    [Fact]
    public async Task ChangePassword_YanlisMevcutParola_400VeBasarisizDenemeKaydedilir()
    {
        var f = new Fixture();
        var user = f.ActiveUser();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.ChangePasswordAsync(1, new ChangePasswordRequestDto { CurrentPassword = "Yanlis123", NewPassword = NewPassword }));

        Assert.True(Fixture.PasswordMatches(user, OldPassword)); // parola değişmedi
        f.Tracker.Verify(t => t.RecordFailure("elif@mail.com"), Times.Once);
        f.RefreshTokens.Verify(r => r.RevokeAllByUserIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_HesapKilitliyse_TooManyRequestsFirlatirVeParolayiDenemez()
    {
        var f = new Fixture();
        var user = f.ActiveUser();
        f.Tracker.Setup(t => t.IsLockedOut("elif@mail.com")).Returns(true);

        await Assert.ThrowsAsync<TooManyRequestsException>(
            () => f.Service.ChangePasswordAsync(1, new ChangePasswordRequestDto { CurrentPassword = OldPassword, NewPassword = NewPassword }));

        // Kilitliyken DOĞRU parola bile işe yaramaz (giriş ekranıyla aynı koruma).
        Assert.True(Fixture.PasswordMatches(user, OldPassword));
        f.Tracker.Verify(t => t.RecordFailure(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_YeniParolaEskisiyleAyniysa_InvalidOperationFirlatir()
    {
        var f = new Fixture();
        f.ActiveUser();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.ChangePasswordAsync(1, new ChangePasswordRequestDto { CurrentPassword = OldPassword, NewPassword = OldPassword }));
    }

    [Fact]
    public async Task ChangePassword_AktifOlmayanKullanici_UnauthorizedFirlatir()
    {
        var f = new Fixture();
        f.ActiveUser(UserStatus.Inactive);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Service.ChangePasswordAsync(1, new ChangePasswordRequestDto { CurrentPassword = OldPassword, NewPassword = NewPassword }));
    }

    [Fact]
    public async Task ChangePassword_Basarili_ParolaDegisirTumOturumlarKapanirYeniTokenDoner()
    {
        var f = new Fixture();
        var user = f.ActiveUser();

        var result = await f.Service.ChangePasswordAsync(1, new ChangePasswordRequestDto { CurrentPassword = OldPassword, NewPassword = NewPassword });

        Assert.True(Fixture.PasswordMatches(user, NewPassword));
        Assert.False(Fixture.PasswordMatches(user, OldPassword));
        // Diğer cihazlardaki tüm refresh token'lar iptal edilir...
        f.RefreshTokens.Verify(r => r.RevokeAllByUserIdAsync(1), Times.Once);
        f.Tracker.Verify(t => t.Reset("elif@mail.com"), Times.Once);
        // ...ama değiştiren oturum çıkış yapmasın diye ona yeni çift verilir (iptalden SONRA üretilir).
        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        f.RefreshTokens.Verify(r => r.AddAsync(It.IsAny<RefreshToken>()), Times.Once);
    }

    // ================================================================ şifremi unuttum

    [Fact]
    public async Task Forgot_KayitsizEposta_SessizceBiter_TokenYokEpostaYok()
    {
        var f = new Fixture();
        f.Users.Setup(r => r.GetByEmailAsync("yok@mail.com")).ReturnsAsync((User?)null);

        await f.Service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "yok@mail.com" });

        // Hata vermez ve hiçbir iz bırakmaz: böylece dışarıdan "bu adres kayıtlı mı" anlaşılamaz.
        f.ResetTokens.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>()), Times.Never);
        f.Email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(UserStatus.Pending)]
    [InlineData(UserStatus.Inactive)]
    public async Task Forgot_AktifOlmayanHesap_SessizceBiter(UserStatus status)
    {
        var f = new Fixture();
        f.ActiveUser(status);

        await f.Service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "elif@mail.com" });

        f.ResetTokens.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>()), Times.Never);
        f.Email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Forgot_AktifHesap_OzetSaklanirHamAnahtarYalnizcaEpostadaDoner()
    {
        var f = new Fixture();
        f.ActiveUser();
        PasswordResetToken? saved = null;
        string? body = null;
        string? to = null;
        f.ResetTokens.Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>())).Callback<PasswordResetToken>(t => saved = t).Returns(Task.CompletedTask);
        f.Email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((t, _, b) => { to = t; body = b; }).Returns(Task.CompletedTask);

        await f.Service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "  elif@mail.com " });

        Assert.NotNull(saved);
        Assert.Equal("elif@mail.com", to);
        // E-postadaki bağlantı: yapılandırılmış adres (sondaki / atılmış) + ham anahtar.
        const string prefix = "https://pusula.example.com/sifre-sifirla?token=";
        Assert.Contains(prefix, body);
        string raw = body![(body.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length)..].Split('\n')[0].Trim();
        Assert.True(raw.Length >= 40);
        Assert.DoesNotMatch("[+/=]", raw); // URL'de kaçış gerektirmeyen anahtar
        // Veritabanına HAM anahtar değil, özeti yazılır.
        Assert.Equal(TokenHasher.Hash(raw), saved!.TokenHash);
        Assert.NotEqual(raw, saved.TokenHash);
        Assert.Equal(1, saved.UserId);
        Assert.InRange(saved.ExpiresAt, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
        Assert.Null(saved.UsedAt);
        // Önceki bağlantılar geçersiz kılınır (sadece en son istenen çalışır).
        f.ResetTokens.Verify(r => r.InvalidateActiveForUserAsync(1, It.IsAny<DateTime>()), Times.Once);
        f.ResetTokens.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ================================================================ parola sıfırlama

    private static PasswordResetToken ValidToken(string raw, DateTime? expires = null, DateTime? used = null) => new()
    {
        Id = 5, UserId = 1, TokenHash = TokenHasher.Hash(raw),
        ExpiresAt = expires ?? DateTime.UtcNow.AddMinutes(10), UsedAt = used
    };

    [Fact]
    public async Task Reset_BilinmeyenAnahtar_InvalidOperationFirlatir()
    {
        var f = new Fixture();
        f.ResetTokens.Setup(r => r.GetByHashAsync(It.IsAny<string>())).ReturnsAsync((PasswordResetToken?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = "uydurma", NewPassword = NewPassword }));
    }

    [Fact]
    public async Task Reset_SuresiDolmusAnahtar_InvalidOperationFirlatirParolaDegismez()
    {
        var f = new Fixture();
        var user = f.ActiveUser();
        f.ResetTokens.Setup(r => r.GetByHashAsync(TokenHasher.Hash("abc"))).ReturnsAsync(ValidToken("abc", expires: DateTime.UtcNow.AddMinutes(-1)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = "abc", NewPassword = NewPassword }));
        Assert.True(Fixture.PasswordMatches(user, OldPassword));
    }

    [Fact]
    public async Task Reset_ZatenKullanilmisAnahtar_InvalidOperationFirlatir()
    {
        var f = new Fixture();
        var user = f.ActiveUser();
        f.ResetTokens.Setup(r => r.GetByHashAsync(TokenHasher.Hash("abc"))).ReturnsAsync(ValidToken("abc", used: DateTime.UtcNow.AddMinutes(-2)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = "abc", NewPassword = NewPassword }));
        Assert.True(Fixture.PasswordMatches(user, OldPassword));
    }

    [Fact]
    public async Task Reset_HesapArtikAktifDegilse_InvalidOperationFirlatir()
    {
        var f = new Fixture();
        f.ActiveUser(UserStatus.Inactive);
        f.ResetTokens.Setup(r => r.GetByHashAsync(TokenHasher.Hash("abc"))).ReturnsAsync(ValidToken("abc"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = "abc", NewPassword = NewPassword }));
    }

    [Fact]
    public async Task Reset_Basarili_ParolaDegisirAnahtarKullanilmisOlurOturumlarKapanirKilitKalkar()
    {
        var f = new Fixture();
        var user = f.ActiveUser();
        var token = ValidToken("abc");
        f.ResetTokens.Setup(r => r.GetByHashAsync(TokenHasher.Hash("abc"))).ReturnsAsync(token);

        // Baştaki/sondaki boşluk (kopyala-yapıştır) sorun çıkarmamalı.
        await f.Service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = "  abc  ", NewPassword = NewPassword });

        Assert.True(Fixture.PasswordMatches(user, NewPassword));
        Assert.NotNull(token.UsedAt); // tek kullanımlık: ikinci kez çalışmaz
        f.Users.Verify(r => r.SaveChangesAsync(), Times.Once);
        f.RefreshTokens.Verify(r => r.RevokeAllByUserIdAsync(1), Times.Once);
        f.Tracker.Verify(t => t.Reset("elif@mail.com"), Times.Once);
    }

    [Fact]
    public async Task Reset_AyniAnahtarIkinciKezKullanilamaz()
    {
        var f = new Fixture();
        f.ActiveUser();
        var token = ValidToken("abc");
        f.ResetTokens.Setup(r => r.GetByHashAsync(TokenHasher.Hash("abc"))).ReturnsAsync(token);

        await f.Service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = "abc", NewPassword = NewPassword });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = "abc", NewPassword = "BaskaParola3" }));
    }

    // ================================================================ e-posta yokken ve yönetici destekli sıfırlama

    [Fact]
    public void IsEmailEnabled_GonderenninYapilandirmaDurumunuYansitir()
    {
        var f = new Fixture();
        Assert.True(f.Service.IsEmailEnabled);

        f.Email.SetupGet(e => e.IsConfigured).Returns(false);
        Assert.False(f.Service.IsEmailEnabled);
    }

    [Fact]
    public async Task Forgot_EpostaYapilandirilmamissa_HicbirSeyYapmaz_BaglantiUretilmezLogaYazilmaz()
    {
        var f = new Fixture();
        f.ActiveUser();
        f.Email.SetupGet(e => e.IsConfigured).Returns(false);

        await f.Service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "elif@mail.com" });

        // Canlıda SMTP yoksa bile anonim istek geçerli bir sıfırlama bağlantısı üretip loga yazmamalı.
        f.ResetTokens.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>()), Times.Never);
        f.Email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task YoneticiBaglantisi_KullaniciYoksa_NotFoundExceptionFirlatir()
    {
        var f = new Fixture();
        f.Users.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.CreateResetLinkForUserAsync(99));
    }

    [Theory]
    [InlineData(UserStatus.Pending)]
    [InlineData(UserStatus.Inactive)]
    public async Task YoneticiBaglantisi_AktifOlmayanKullanici_InvalidOperationExceptionFirlatir(UserStatus status)
    {
        var f = new Fixture();
        f.ActiveUser(status);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateResetLinkForUserAsync(1));
        f.ResetTokens.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>()), Times.Never);
    }

    [Fact]
    public async Task YoneticiBaglantisi_EpostaKapaliykenDeUretilir_HamAnahtarYalnizcaCevaptaOzetVeritabaninda_EpostaGonderilmez()
    {
        var f = new Fixture();
        f.ActiveUser();
        f.Email.SetupGet(e => e.IsConfigured).Returns(false); // asıl kullanım senaryosu: SMTP yok
        PasswordResetToken? saved = null;
        f.ResetTokens.Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>())).Callback<PasswordResetToken>(t => saved = t).Returns(Task.CompletedTask);

        var result = await f.Service.CreateResetLinkForUserAsync(1);

        const string prefix = "https://pusula.example.com/sifre-sifirla?token=";
        Assert.StartsWith(prefix, result.Link);
        string raw = result.Link[prefix.Length..];
        Assert.True(raw.Length >= 40);
        Assert.Equal(1, result.UserId);
        Assert.Equal("Elif Saraç", result.UserName);
        Assert.NotNull(saved);
        Assert.Equal(TokenHasher.Hash(raw), saved!.TokenHash); // veritabanında yalnızca özet
        Assert.NotEqual(raw, saved.TokenHash);
        // Elle iletileceği için 24 saat geçerli.
        Assert.InRange(result.ExpiresAt, DateTime.UtcNow.AddHours(23.9), DateTime.UtcNow.AddHours(24.1));
        Assert.Equal(result.ExpiresAt, saved.ExpiresAt);
        f.ResetTokens.Verify(r => r.InvalidateActiveForUserAsync(1, It.IsAny<DateTime>()), Times.Once); // önceki bağlantılar ölür
        f.Email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task YoneticiBaglantisi_UretilenAnahtarSifirlamadaCalisir()
    {
        var f = new Fixture();
        var user = f.ActiveUser();
        PasswordResetToken? saved = null;
        f.ResetTokens.Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>())).Callback<PasswordResetToken>(t => saved = t).Returns(Task.CompletedTask);
        var result = await f.Service.CreateResetLinkForUserAsync(1);
        string raw = result.Link[(result.Link.IndexOf("token=", StringComparison.Ordinal) + 6)..];
        f.ResetTokens.Setup(r => r.GetByHashAsync(TokenHasher.Hash(raw))).ReturnsAsync(saved);

        await f.Service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = raw, NewPassword = NewPassword });

        Assert.True(Fixture.PasswordMatches(user, NewPassword));
        Assert.NotNull(saved!.UsedAt);
    }
}
