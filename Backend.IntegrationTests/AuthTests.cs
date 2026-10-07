using System.Net;
using System.Net.Http.Json;
using Backend.Dtos;
using Backend.Entities;
using Backend.Services;

namespace Backend.IntegrationTests;

// Kayıt, giriş, refresh token döndürme (rotation), çıkış ve hesap kilidi: gerçek veritabanı + gerçek JWT ile.
[Collection("api")]
public class AuthTests
{
    private readonly TestWorld _world;

    public AuthTests(ApiFactory factory)
    {
        _world = new TestWorld(factory);
    }

    [Fact]
    public async Task Kayit_Pending_Olusur_Onaydan_Once_GirisYapilamaz_Onaydan_Sonra_Yapilir()
    {
        var user = await _world.RegisterAsync("Yeni Stajyer");

        // Onaysız hesap: parola doğru olsa bile giriş yok (401).
        var pendingLogin = await _world.Anonymous().PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Email = user.Email, Password = TestUser.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, pendingLogin.StatusCode);

        await _world.SetStateAsync(user, UserRole.Intern, UserStatus.Active);
        var auth = await _world.LoginAsync(user);

        Assert.Equal("Intern", auth.User.Role);
        Assert.Equal("Active", auth.User.Status);
        var me = await TestWorld.ReadAsync<UserDto>(await user.Client.GetAsync("/api/users/me"));
        Assert.Equal(user.Email, me.Email);
    }

    [Fact]
    public async Task Kayit_RolAlaniGonderilse_YokSayilir_HerkesStajyerOlarakBaslar()
    {
        // Kitlesel atama (over-posting) denemesi: istek gövdesine "role": "Admin" eklenir.
        var email = _world.UniqueEmail("hile");
        var response = await _world.Anonymous().PostAsJsonAsync("/api/auth/register",
            new { fullName = "Hileci Kisi", email, password = TestUser.Password, role = "Admin", status = "Active" });

        var created = await TestWorld.ReadAsync<UserDto>(response, HttpStatusCode.Created);
        Assert.Equal("Intern", created.Role);
        Assert.Equal("Pending", created.Status);
    }

    [Fact]
    public async Task Kayit_AyniEposta_409_ZayifParola_400()
    {
        var user = await _world.RegisterAsync("Ilk Kayit");

        var duplicate = await _world.Anonymous().PostAsJsonAsync("/api/auth/register",
            new RegisterRequestDto { FullName = "Ikinci", Email = user.Email.ToUpperInvariant(), Password = TestUser.Password });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        foreach (var weak in new[] { "kisa1A", "kucukharf123", "BUYUKHARF123", "SadeceHarfler" })
        {
            var response = await _world.Anonymous().PostAsJsonAsync("/api/auth/register",
                new RegisterRequestDto { FullName = "Zayif Parola", Email = _world.UniqueEmail("zayif"), Password = weak });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Giris_YanlisParola_401_BilinmeyenKullanici_401()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);

        var wrong = await _world.Anonymous().PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Email = user.Email, Password = "YanlisParola1" });
        var unknown = await _world.Anonymous().PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Email = _world.UniqueEmail("yok"), Password = TestUser.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task Giris_ArdArdaHataliDeneme_HesabiKilitler_DogruParolaBileGirisVermez()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);

        for (int i = 0; i < LoginAttemptTracker.MaxFailures; i++)
        {
            await _world.Anonymous().PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = user.Email, Password = "Yanlis" + i + "Aa" });
        }

        // Kilitliyken DOĞRU parola bile 429 alır (kaba kuvvet saldırısına karşı hesap bazlı koruma).
        var locked = await _world.Anonymous().PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Email = user.Email, Password = TestUser.Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
    }

    [Fact]
    public async Task Refresh_TokenDonerVeTekKullanimliktir_EskisiIkinciKezKullanilamaz()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);
        var first = user.RefreshToken;

        var refreshed = await TestWorld.ReadAsync<AuthResponseDto>(
            await _world.Anonymous().PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto { RefreshToken = first }));

        Assert.NotEqual(first, refreshed.RefreshToken);
        Assert.False(string.IsNullOrEmpty(refreshed.Token));

        // Rotation: kullanılan refresh token ölür. Çalınmış bir token ikinci kez kullanılamaz.
        var reuse = await _world.Anonymous().PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto { RefreshToken = first });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // Yeni çift çalışır.
        var next = await _world.Anonymous().PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto { RefreshToken = refreshed.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task Refresh_UydurmaToken_401()
    {
        var response = await _world.Anonymous().PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto { RefreshToken = "uydurma-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cikis_RefreshTokeniIptalEder()
    {
        var user = await _world.CreateActiveAsync(UserRole.Intern);

        var logout = await _world.Anonymous().PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequestDto { RefreshToken = user.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await _world.Anonymous().PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto { RefreshToken = user.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task KorumaliUcNokta_TokenYok_401_BozukToken_401()
    {
        var anonymous = await _world.Anonymous().GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var client = _world.Anonymous();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "bozuk.token.degeri");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }
}
