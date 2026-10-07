using System.Net;
using System.Net.Http.Json;
using Backend.Dtos;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.IntegrationTests;

// Parola değiştirme, "şifremi unuttum" ve sıfırlama: güvenlik özellikleri gerçek veritabanı ve e-posta yakalayıcıyla.
[Collection("api")]
public class PasswordTests
{
    private const string NewPassword = "YeniParola2";

    private readonly ApiFactory _factory;
    private readonly TestWorld _world;

    public PasswordTests(ApiFactory factory)
    {
        _factory = factory;
        _world = new TestWorld(factory);
    }

    private Task<HttpResponseMessage> ChangeAsync(TestUser user, string current, string next) =>
        user.Client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequestDto { CurrentPassword = current, NewPassword = next });

    private Task<HttpResponseMessage> ForgotAsync(string email) =>
        _world.Anonymous().PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = email });

    private Task<HttpResponseMessage> ResetAsync(string token, string password) =>
        _world.Anonymous().PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto { Token = token, NewPassword = password });

    private async Task<bool> CanLoginAsync(TestUser user, string password) =>
        (await _world.Anonymous().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = user.Email, Password = password })).IsSuccessStatusCode;

    // ============================================================ parola değiştirme

    [Fact]
    public async Task Degistir_TokenYoksa_401()
    {
        var response = await _world.Anonymous().PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequestDto { CurrentPassword = TestUser.Password, NewPassword = NewPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Degistir_YanlisMevcutParola_400_Istemci401SanipCikisYapmasin_ParolaDegismez()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);

        var response = await ChangeAsync(user, "Yanlis999a", NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await CanLoginAsync(user, TestUser.Password));
    }

    [Theory]
    [InlineData("kisa1A")]
    [InlineData("kucukharf123")]
    [InlineData("BUYUKHARF123")]
    [InlineData("SadeceHarfler")]
    public async Task Degistir_ZayifYeniParola_400(string weak)
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);

        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(user, TestUser.Password, weak)).StatusCode);
    }

    [Fact]
    public async Task Degistir_YeniParolaEskisiyleAyni_400()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);

        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(user, TestUser.Password, TestUser.Password)).StatusCode);
    }

    [Fact]
    public async Task Degistir_Basarili_EskiParolaCalismaz_YeniCalisir_DigerCihazlarKapanir_BuOturumDevamEder()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);
        var otherDeviceRefresh = (await _world.LoginAsync(user)).RefreshToken; // "diğer cihaz"ın oturumu
        var thisDevice = await _world.LoginAsync(user);

        var response = await ChangeAsync(user, TestUser.Password, NewPassword);
        var fresh = await TestWorld.ReadAsync<AuthResponseDto>(response);

        Assert.False(await CanLoginAsync(user, TestUser.Password));
        Assert.True(await CanLoginAsync(user, NewPassword));

        // Diğer cihazın refresh token'ı iptal edildi...
        var other = await _world.Anonymous().PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto { RefreshToken = otherDeviceRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
        var old = await _world.Anonymous().PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto { RefreshToken = thisDevice.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        // ...ama değişimi yapan oturum, cevapta gelen yeni çiftle devam eder.
        var next = await _world.Anonymous().PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto { RefreshToken = fresh.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task Degistir_YanlisMevcutParolaTekrarlanirsa_HesapKilitlenir_429()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);

        for (int i = 0; i < LoginAttemptTracker.MaxFailures; i++)
        {
            await ChangeAsync(user, "Yanlis" + i + "aA", NewPassword);
        }

        // Kilitliyken DOĞRU mevcut parola bile işe yaramaz: parola değiştirme ekranı kaba kuvvetle denenemez.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await ChangeAsync(user, TestUser.Password, NewPassword)).StatusCode);
    }

    // ============================================================ şifremi unuttum

    [Fact]
    public async Task Unuttum_KayitsizOnayBekleyenVePasifHesap_AyniCevap_EpostaGonderilmez()
    {
        var pending = await _world.RegisterAsync("Bekleyen");
        var inactive = await _world.CreateActiveAsync(UserRole.Intern);
        await _world.SetStateAsync(inactive, UserRole.Intern, UserStatus.Inactive);
        var active = await _world.CreateActiveAsync(UserRole.Intern);

        string unknownBody = await (await ForgotAsync(_world.UniqueEmail("yok"))).Content.ReadAsStringAsync();
        foreach (var email in new[] { pending.Email, inactive.Email })
        {
            var response = await ForgotAsync(email);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(unknownBody, await response.Content.ReadAsStringAsync());
            Assert.Empty(_factory.Emails.To(email));
        }

        // Kayıtlı hesap için de cevap AYNI: dışarıdan "bu adres kayıtlı mı" anlaşılamaz.
        var activeResponse = await ForgotAsync(active.Email);
        Assert.Equal(unknownBody, await activeResponse.Content.ReadAsStringAsync());
        Assert.Single(_factory.Emails.To(active.Email));
    }

    [Fact]
    public async Task Unuttum_GecersizEpostaBicimi_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await ForgotAsync("eposta-degil")).StatusCode);
    }

    [Fact]
    public async Task Unuttum_VeritabaninaHamAnahtarYazilmaz_YalnizcaOzetSaklanir()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);
        await ForgotAsync(user.Email);
        string raw = _factory.Emails.LastResetToken(user.Email)!;

        var stored = await _world.WithDbAsync(db => db.PasswordResetTokens.Where(t => t.UserId == user.Id).ToListAsync());

        var token = Assert.Single(stored);
        Assert.NotEqual(raw, token.TokenHash);
        Assert.Equal(TokenHasher.Hash(raw), token.TokenHash);
        Assert.InRange(token.ExpiresAt, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
        Assert.Null(token.UsedAt);
        // E-postadaki bağlantı yapılandırılmış ön yüz adresini kullanır.
        Assert.Contains("https://pusula.test/sifre-sifirla?token=", _factory.Emails.To(user.Email).Last().Body);
    }

    // ============================================================ sıfırlama

    [Fact]
    public async Task Sifirla_Basarili_YeniParolaCalisir_EskisiCalismaz_TumOturumlarKapanir()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);
        string refresh = user.RefreshToken;
        await ForgotAsync(user.Email);

        var response = await ResetAsync(_factory.Emails.LastResetToken(user.Email)!, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await CanLoginAsync(user, TestUser.Password));
        Assert.True(await CanLoginAsync(user, NewPassword));
        // Hesap ele geçirildiyse saldırganın açık oturumu da kapanır.
        var old = await _world.Anonymous().PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto { RefreshToken = refresh });
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
    }

    [Fact]
    public async Task Sifirla_AnahtarTekKullanimlik_IkinciKezKullanilamaz()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);
        await ForgotAsync(user.Email);
        string token = _factory.Emails.LastResetToken(user.Email)!;

        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(token, NewPassword)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, "BaskaParola3")).StatusCode);
        Assert.True(await CanLoginAsync(user, NewPassword)); // ikinci deneme parolayı değiştirmedi
    }

    [Fact]
    public async Task Sifirla_YenidenIstenirse_EskiBaglantiOlur_YalnizcaSonGelenCalisir()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);
        await ForgotAsync(user.Email);
        string first = _factory.Emails.LastResetToken(user.Email)!;
        await ForgotAsync(user.Email);
        string second = _factory.Emails.LastResetToken(user.Email)!;

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(first, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(second, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Sifirla_SuresiDolmusAnahtar_400()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);
        await ForgotAsync(user.Email);
        string token = _factory.Emails.LastResetToken(user.Email)!;
        await _world.WithDbAsync(async db =>
        {
            var row = await db.PasswordResetTokens.SingleAsync(t => t.UserId == user.Id);
            row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, NewPassword)).StatusCode);
        Assert.True(await CanLoginAsync(user, TestUser.Password));
    }

    [Fact]
    public async Task Sifirla_BaglantiSonrasiHesapPasiflestirilirse_400()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);
        await ForgotAsync(user.Email);
        string token = _factory.Emails.LastResetToken(user.Email)!;
        await _world.SetStateAsync(user, UserRole.Intern, UserStatus.Inactive);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, NewPassword)).StatusCode);
    }

    [Theory]
    [InlineData("uydurma-anahtar", "YeniParola2")]
    [InlineData("", "YeniParola2")]
    [InlineData("x", "zayif")]
    public async Task Sifirla_UydurmaAnahtarVeZayifParola_400(string token, string password)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, password)).StatusCode);
    }

    [Fact]
    public async Task Sifirla_KilitliHesabinKilidiniKaldirir()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);
        for (int i = 0; i < LoginAttemptTracker.MaxFailures; i++)
        {
            await _world.Anonymous().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = user.Email, Password = "Yanlis" + i + "aA" });
        }
        Assert.False(await CanLoginAsync(user, TestUser.Password)); // kilitli

        await ForgotAsync(user.Email);
        await ResetAsync(_factory.Emails.LastResetToken(user.Email)!, NewPassword);

        // Parolasını unutup kilitlenen kullanıcı sıfırlamayla tekrar girebilir.
        Assert.True(await CanLoginAsync(user, NewPassword));
    }
}
