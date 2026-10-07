using System.Net;
using System.Net.Http.Json;
using Backend.Dtos;
using Backend.Entities;

namespace Backend.IntegrationTests;

// Rol bazlı kısıtlar, anlık yetki geçerliliği (JWT'ye rol gömülmediği için) ve Admin işlemleri.
[Collection("api")]
public class AccessControlTests
{
    private readonly TestWorld _world;

    public AccessControlTests(ApiFactory factory)
    {
        _world = new TestWorld(factory);
    }

    // Her uç nokta için yalnızca belirtilen roller girebilir. (Mentor sahiplik kontrolleri ayrıca ilgili testlerde.)
    public static IEnumerable<object[]> RoleGatedEndpoints() =>
    [
        ["GET", "/api/admin/users", UserRole.Admin],
        ["GET", "/api/admin/groups", UserRole.Admin],
        ["GET", "/api/admin/tasks", UserRole.Admin],
        ["GET", "/api/groups/mine", UserRole.Mentor],
        ["GET", "/api/tasks/created", UserRole.Mentor],
        ["GET", "/api/users/interns", UserRole.Mentor],
        ["GET", "/api/users/my-interns", UserRole.Mentor],
        ["GET", "/api/notes/review", UserRole.Mentor],
    ];

    [Theory]
    [MemberData(nameof(RoleGatedEndpoints))]
    public async Task RolKapisi_YalnizcaIzinliRolGirer_DigerleriIcin403_Anonim401(string method, string url, UserRole allowed)
    {
        foreach (var role in new[] { UserRole.Intern, UserRole.Mentor, UserRole.Admin })
        {
            var user = await _world.CreateActiveAsync(role);
            var response = await user.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

            if (role == allowed)
            {
                Assert.True(response.IsSuccessStatusCode, $"{role} → {url} beklenen başarılı, gelen {(int)response.StatusCode}");
            }
            else
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }

        var anonymous = await _world.Anonymous().SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task PasifleStirilenKullanici_ElindekiTokenIleBileBirSonrakiIstekteReddedilir()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        Assert.Equal(HttpStatusCode.OK, (await intern.Client.GetAsync("/api/users/me")).StatusCode);

        var deactivate = await admin.Client.PostAsync($"/api/admin/deactivate-user/{intern.Id}", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        // Access token'ın süresi dolmadı (1 saat), ama kullanıcı her istekte veritabanından kontrol edilir.
        Assert.Equal(HttpStatusCode.Unauthorized, (await intern.Client.GetAsync("/api/users/me")).StatusCode);
        // Yeniden giriş de yapamaz.
        var relogin = await _world.Anonymous().PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Email = intern.Email, Password = TestUser.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, relogin.StatusCode);
    }

    [Fact]
    public async Task RolDegisimi_Aninda_Gecerli_EskiTokenYeniRoleGoreCalisir()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        var mentor = await _world.CreateActiveAsync(UserRole.Mentor);
        Assert.Equal(HttpStatusCode.OK, (await mentor.Client.GetAsync("/api/groups/mine")).StatusCode);

        var change = await admin.Client.PutAsJsonAsync($"/api/admin/users/{mentor.Id}/role", new UpdateUserRoleRequestDto { Role = "Intern" });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        // Aynı token: rol token'da değil, her istekte veritabanından okunduğu için yetki hemen düştü.
        Assert.Equal(HttpStatusCode.Forbidden, (await mentor.Client.GetAsync("/api/groups/mine")).StatusCode);
    }

    [Fact]
    public async Task Admin_KendiRolunuDegistiremez_GecersizRolReddedilir()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        var other = await _world.CreateActiveAsync(UserRole.Intern);

        var self = await admin.Client.PutAsJsonAsync($"/api/admin/users/{admin.Id}/role", new UpdateUserRoleRequestDto { Role = "Intern" });
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);

        foreach (var invalid in new[] { "SuperAdmin", "99", "" })
        {
            var response = await admin.Client.PutAsJsonAsync($"/api/admin/users/{other.Id}/role", new UpdateUserRoleRequestDto { Role = invalid });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Admin_BekleyenKullaniciyiOnaylar_KullaniciGirisYapabilir()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        var pending = await _world.RegisterAsync("Bekleyen Stajyer");

        var pendingList = await TestWorld.ReadAsync<PagedResultDto<UserDto>>(await admin.Client.GetAsync("/api/admin/users/pending?search=" + Uri.EscapeDataString(pending.Email)));
        Assert.Single(pendingList.Items);

        var approve = await admin.Client.PostAsync($"/api/admin/approve-user/{pending.Id}", null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var auth = await _world.LoginAsync(pending);
        Assert.Equal("Active", auth.User.Status);
        var after = await TestWorld.ReadAsync<PagedResultDto<UserDto>>(await admin.Client.GetAsync("/api/admin/users/pending?search=" + Uri.EscapeDataString(pending.Email)));
        Assert.Empty(after.Items);
    }

    [Fact]
    public async Task Admin_OlmayanKullaniciyaIslem_404()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.PostAsync("/api/admin/approve-user/999999", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.PostAsync("/api/admin/deactivate-user/999999", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.GetAsync("/api/admin/users/999999")).StatusCode);
    }

    [Fact]
    public async Task Admin_GorevAtayamaz_GrupUyeleriniListeleyemez_BilincliKisit()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();
        var admin = await _world.CreateActiveAsync(UserRole.Admin);

        // Admin sistemi görüntüler ve yönetir; günlük işi (görev atama, grup üyeliği) mentor yürütür.
        var assign = await admin.Client.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequestDto { Title = "Admin görevi", AssignedUserId = intern.Id });
        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.GetAsync($"/api/groups/{group.Id}/members")).StatusCode);
    }

    [Fact]
    public async Task Profil_YalnizcaAdGuncellenir_RolVeDurumDegismez()
    {
        var intern = await _world.CreateActiveAsync(UserRole.Intern);

        var response = await intern.Client.PutAsJsonAsync("/api/users/me",
            new { fullName = "Yeni Ad Soyad", role = "Admin", status = "Active", email = "baska@test.local" });
        var updated = await TestWorld.ReadAsync<UserDto>(response);

        Assert.Equal("Yeni Ad Soyad", updated.FullName);
        Assert.Equal("Intern", updated.Role);
        Assert.Equal(intern.Email, updated.Email);
    }
}
