using System.Net;
using System.Net.Http.Json;
using Backend.Dtos;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.IntegrationTests;

// E-posta servisi olmadan parola sıfırlama: yönetici kullanıcı için tek kullanımlık bağlantı üretir ve elle iletir.
[Collection("api")]
public class AdminResetLinkTests
{
    private const string NewPassword = "YeniParola2";

    private readonly ApiFactory _factory;
    private readonly TestWorld _world;

    public AdminResetLinkTests(ApiFactory factory)
    {
        _factory = factory;
        _world = new TestWorld(factory);
    }

    private static string TokenOf(PasswordResetLinkDto link) => link.Link[(link.Link.IndexOf("token=", StringComparison.Ordinal) + 6)..];

    private static async Task<PasswordResetLinkDto> CreateLinkAsync(TestUser admin, TestUser target) =>
        await TestWorld.ReadAsync<PasswordResetLinkDto>(await admin.Client.PostAsync($"/api/admin/users/{target.Id}/reset-link", null));

    private Task<HttpResponseMessage> ResetAsync(string token, string password = NewPassword) =>
        _world.Anonymous().PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto { Token = token, NewPassword = password });

    private async Task<bool> CanLoginAsync(TestUser user, string password) =>
        (await _world.Anonymous().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = user.Email, Password = password })).IsSuccessStatusCode;

    [Fact]
    public async Task Yonetici_BaglantiUretir_EpostaGitmez_BaglantiAdresiVeSureDogru()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        var intern = await _world.CreateActiveAsync(UserRole.Intern, "Parolasini Unutan");

        var link = await CreateLinkAsync(admin, intern);

        Assert.Equal(intern.Id, link.UserId);
        Assert.Equal("Parolasini Unutan", link.UserName);
        Assert.StartsWith("https://pusula.test/sifre-sifirla?token=", link.Link);
        Assert.InRange(link.ExpiresAt, DateTime.UtcNow.AddHours(23), DateTime.UtcNow.AddHours(25));
        Assert.Empty(_factory.Emails.To(intern.Email)); // bağlantı e-postayla gitmez, yöneticiye döner
    }

    [Fact]
    public async Task Baglanti_UretmekParolayiVeOturumlariDegistirmez_KullanilincaDegistirir()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        string refresh = intern.RefreshToken;

        var link = await CreateLinkAsync(admin, intern);

        // Yalnızca bağlantı üretildi: stajyerin parolası ve açık oturumu hâlâ geçerli.
        Assert.True(await CanLoginAsync(intern, TestUser.Password));
        var stillValid = await _world.Anonymous().PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto { RefreshToken = refresh });
        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(TokenOf(link))).StatusCode);

        Assert.False(await CanLoginAsync(intern, TestUser.Password));
        Assert.True(await CanLoginAsync(intern, NewPassword));
    }

    [Fact]
    public async Task Baglanti_TekKullanimlik_IkinciKezCalismaz()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        string token = TokenOf(await CreateLinkAsync(admin, intern));

        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(token)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, "BaskaParola3")).StatusCode);
        Assert.True(await CanLoginAsync(intern, NewPassword));
    }

    [Fact]
    public async Task YenidenUretilince_EskiBaglantiOlur_YalnizcaSonuCalisir()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        string first = TokenOf(await CreateLinkAsync(admin, intern));
        string second = TokenOf(await CreateLinkAsync(admin, intern));

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(second)).StatusCode);
    }

    [Fact]
    public async Task VeritabaniHamAnahtariTutmaz_YalnizcaOzet()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        string raw = TokenOf(await CreateLinkAsync(admin, intern));

        var row = Assert.Single(await _world.WithDbAsync(db => db.PasswordResetTokens.Where(t => t.UserId == intern.Id).ToListAsync()));

        Assert.NotEqual(raw, row.TokenHash);
        Assert.Equal(TokenHasher.Hash(raw), row.TokenHash);
    }

    [Fact]
    public async Task Yetki_SadeceAdmin_MentorStajyerVeAnonim_Reddedilir()
    {
        var target = await _world.CreateActiveAsync(UserRole.Intern);
        foreach (var role in new[] { UserRole.Intern, UserRole.Mentor })
        {
            var user = await _world.CreateActiveAsync(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostAsync($"/api/admin/users/{target.Id}/reset-link", null)).StatusCode);
        }

        var anonymous = await _world.Anonymous().PostAsync($"/api/admin/users/{target.Id}/reset-link", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        // Reddedilen denemeler hiçbir bağlantı üretmedi.
        Assert.Empty(await _world.WithDbAsync(db => db.PasswordResetTokens.Where(t => t.UserId == target.Id).ToListAsync()));
    }

    [Fact]
    public async Task OlmayanKullanici404_OnayBekleyenVePasifKullanici400()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        var pending = await _world.RegisterAsync("Bekleyen");
        var inactive = await _world.CreateActiveAsync(UserRole.Intern);
        await _world.SetStateAsync(inactive, UserRole.Intern, UserStatus.Inactive);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.PostAsync("/api/admin/users/999999/reset-link", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PostAsync($"/api/admin/users/{pending.Id}/reset-link", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PostAsync($"/api/admin/users/{inactive.Id}/reset-link", null)).StatusCode);
    }

    [Fact]
    public async Task Yapilandirma_HerkeseAcik_EpostaTanimliysaTrueDoner()
    {
        var response = await _world.Anonymous().GetAsync("/api/auth/config");

        var config = await TestWorld.ReadAsync<PublicConfigDto>(response);
        Assert.True(config.EmailEnabled);
    }
}

/// <summary>E-posta servisi OLMAYAN bir kurulum: asıl senaryo (SMTP tanımlı değil).</summary>
public sealed class NoEmailApiFactory : ApiFactory
{
    public NoEmailApiFactory()
    {
        Emails.Configured = false;
    }
}

public class NoEmailTests : IClassFixture<NoEmailApiFactory>
{
    private readonly NoEmailApiFactory _factory;
    private readonly TestWorld _world;

    public NoEmailTests(NoEmailApiFactory factory)
    {
        _factory = factory;
        _world = new TestWorld(factory);
    }

    [Fact]
    public async Task Yapilandirma_EpostaTanimliDegilseFalseDoner()
    {
        var config = await TestWorld.ReadAsync<PublicConfigDto>(await _world.Anonymous().GetAsync("/api/auth/config"));

        Assert.False(config.EmailEnabled);
    }

    [Fact]
    public async Task SifremiUnuttum_EpostaYokken_AyniCevapVerirAmaBaglantiUretmezVeEpostaGondermez()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);

        var response = await _world.Anonymous().PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = user.Email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // cevap değişmez: hesap var/yok bilgisi sızmaz
        Assert.Empty(_factory.Emails.To(user.Email));
        Assert.Empty(await _world.WithDbAsync(db => db.PasswordResetTokens.Where(t => t.UserId == user.Id).ToListAsync()));
    }

    [Fact]
    public async Task YoneticiYoluEpostaOlmadanUctanUcaCalisir()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        var intern = await _world.CreateActiveAsync(UserRole.Intern);

        var link = await TestWorld.ReadAsync<PasswordResetLinkDto>(await admin.Client.PostAsync($"/api/admin/users/{intern.Id}/reset-link", null));
        string token = link.Link[(link.Link.IndexOf("token=", StringComparison.Ordinal) + 6)..];
        var reset = await _world.Anonymous().PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto { Token = token, NewPassword = "YeniParola2" });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        var login = await _world.Anonymous().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = intern.Email, Password = "YeniParola2" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Empty(_factory.Emails.All); // hiçbir aşamada e-posta gönderilmedi
    }
}
