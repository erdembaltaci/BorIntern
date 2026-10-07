using System.Net;
using System.Net.Http.Json;
using Backend.Dtos;
using Backend.Entities;

namespace Backend.IntegrationTests;

// Görev atama, durum güncelleme, düzenleme/devir, silme-geri alma, filtre/özet ve sayfalama.
[Collection("api")]
public class TaskTests
{
    private readonly TestWorld _world;

    public TaskTests(ApiFactory factory)
    {
        _world = new TestWorld(factory);
    }

    private async Task<PagedResultDto<TaskDto>> MineAsync(TestUser intern, string query = "") =>
        await TestWorld.ReadAsync<PagedResultDto<TaskDto>>(await intern.Client.GetAsync("/api/tasks/mine" + query));

    private Task<HttpResponseMessage> SetStatusAsync(TestUser intern, int taskId, string status) =>
        intern.Client.PutAsJsonAsync($"/api/tasks/{taskId}/status", new UpdateTaskStatusRequestDto { Status = status });

    [Fact]
    public async Task Ata_KendiGrubundakiStajyereBasarili_StajyerGorevdeAdlariGorur()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();

        var task = await _world.AssignTaskAsync(mentor, intern, "API oku");

        Assert.Equal("Todo", task.Status);
        Assert.Equal(intern.FullName, task.AssignedUserName);
        Assert.Equal(mentor.FullName, task.CreatedByUserName);
        var mine = await MineAsync(intern);
        Assert.Contains(mine.Items, t => t.Id == task.Id);
    }

    [Fact]
    public async Task Ata_GrubundaOlmayanStajyer403_StajyerAtayamaz403_Dogrulama400()
    {
        var (mentor, _, _) = await _world.MentorWithInternAsync();
        var outsider = await _world.CreateActiveAsync(UserRole.Intern);

        var outside = await mentor.Client.PostAsJsonAsync("/api/tasks", new CreateTaskRequestDto { Title = "Görev", AssignedUserId = outsider.Id });
        Assert.Equal(HttpStatusCode.Forbidden, outside.StatusCode);

        var byIntern = await outsider.Client.PostAsJsonAsync("/api/tasks", new CreateTaskRequestDto { Title = "Hile", AssignedUserId = outsider.Id });
        Assert.Equal(HttpStatusCode.Forbidden, byIntern.StatusCode);

        foreach (var invalid in new[]
        {
            new CreateTaskRequestDto { Title = "a", AssignedUserId = outsider.Id },
            new CreateTaskRequestDto { Title = new string('x', 151), AssignedUserId = outsider.Id },
            new CreateTaskRequestDto { Title = "Geçerli", AssignedUserId = 0 }
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await mentor.Client.PostAsJsonAsync("/api/tasks", invalid)).StatusCode);
        }
    }

    [Fact]
    public async Task Durum_SadeceAtananStajyerDegistirir_BaskasiVeMentor403_GecersizDurum400()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();
        var otherIntern = await _world.CreateActiveAsync(UserRole.Intern);
        await _world.AddMemberAsync(mentor, group.Id, otherIntern);
        var task = await _world.AssignTaskAsync(mentor, intern);

        Assert.Equal(HttpStatusCode.Forbidden, (await SetStatusAsync(otherIntern, task.Id, "Completed")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetStatusAsync(mentor, task.Id, "Completed")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatusAsync(intern, task.Id, "Bitti")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetStatusAsync(intern, 999999, "Completed")).StatusCode);

        var ok = await TestWorld.ReadAsync<TaskDto>(await SetStatusAsync(intern, task.Id, "InProgress"));
        Assert.Equal("InProgress", ok.Status);
    }

    [Fact]
    public async Task TekilGorev_AtananVeAtayanGorur_YabanciStajyer403_SilinmisGorev404()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var stranger = await _world.CreateActiveAsync(UserRole.Intern);
        var task = await _world.AssignTaskAsync(mentor, intern);

        Assert.Equal(HttpStatusCode.OK, (await intern.Client.GetAsync($"/api/tasks/{task.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await mentor.Client.GetAsync($"/api/tasks/{task.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.Client.GetAsync($"/api/tasks/{task.Id}")).StatusCode);

        await mentor.Client.DeleteAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NotFound, (await intern.Client.GetAsync($"/api/tasks/{task.Id}")).StatusCode);
    }

    [Fact]
    public async Task Duzenle_BilgilerGuncellenir_DurumKorunur_DevirOlmadigiIcinGrupKontroluYok()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var task = await _world.AssignTaskAsync(mentor, intern, "Eski başlık");
        await SetStatusAsync(intern, task.Id, "InProgress");

        var response = await mentor.Client.PutAsJsonAsync($"/api/tasks/{task.Id}", new UpdateTaskRequestDto
        {
            Title = "  Yeni başlık  ", Description = "Yeni açıklama", DueDate = new DateTime(2031, 5, 4), AssignedUserId = intern.Id
        });

        var updated = await TestWorld.ReadAsync<TaskDto>(response);
        Assert.Equal("Yeni başlık", updated.Title);
        Assert.Equal("Yeni açıklama", updated.Description);
        Assert.Equal(new DateTime(2031, 5, 4), updated.DueDate);
        Assert.Equal("InProgress", updated.Status); // stajyerin ilerlettiği durum bozulmadı
    }

    [Fact]
    public async Task Devret_YeniStajyereGecer_DurumTodoyaDoner_EskiStajyerArtikGormez()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();
        var newIntern = await _world.CreateActiveAsync(UserRole.Intern, "Yeni Stajyer");
        await _world.AddMemberAsync(mentor, group.Id, newIntern);
        var task = await _world.AssignTaskAsync(mentor, intern, "Devredilecek");
        await SetStatusAsync(intern, task.Id, "Completed");

        var response = await mentor.Client.PutAsJsonAsync($"/api/tasks/{task.Id}",
            new UpdateTaskRequestDto { Title = "Devredilecek", AssignedUserId = newIntern.Id });

        var moved = await TestWorld.ReadAsync<TaskDto>(response);
        Assert.Equal(newIntern.Id, moved.AssignedUserId);
        Assert.Equal("Yeni Stajyer", moved.AssignedUserName);
        Assert.Equal("Todo", moved.Status); // yeni stajyer sıfırdan başlar
        Assert.DoesNotContain((await MineAsync(intern)).Items, t => t.Id == task.Id);
        Assert.Contains((await MineAsync(newIntern)).Items, t => t.Id == task.Id);
    }

    [Fact]
    public async Task Devret_GrubundaOlmayanStajyere403_GorevHicDegismez()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var outsider = await _world.CreateActiveAsync(UserRole.Intern);
        var task = await _world.AssignTaskAsync(mentor, intern, "Sabit başlık");

        var response = await mentor.Client.PutAsJsonAsync($"/api/tasks/{task.Id}",
            new UpdateTaskRequestDto { Title = "Değişmemeli", AssignedUserId = outsider.Id });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var unchanged = await TestWorld.ReadAsync<TaskDto>(await mentor.Client.GetAsync($"/api/tasks/{task.Id}"));
        Assert.Equal("Sabit başlık", unchanged.Title);
        Assert.Equal(intern.Id, unchanged.AssignedUserId);
    }

    [Fact]
    public async Task Duzenle_SadeceAtayanMentor_BaskaMentorStajyerVeOlmayanGorev_Reddedilir()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var other = await _world.CreateActiveAsync(UserRole.Mentor);
        var task = await _world.AssignTaskAsync(mentor, intern);
        var body = new UpdateTaskRequestDto { Title = "Hile", AssignedUserId = intern.Id };

        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.PutAsJsonAsync($"/api/tasks/{task.Id}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await intern.Client.PutAsJsonAsync($"/api/tasks/{task.Id}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mentor.Client.PutAsJsonAsync("/api/tasks/999999", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mentor.Client.PutAsJsonAsync($"/api/tasks/{task.Id}", new UpdateTaskRequestDto { Title = "", AssignedUserId = intern.Id })).StatusCode);
    }

    [Fact]
    public async Task SilVeGeriGetir_SadeceAtayanMentor_SilinenGorevStajyereGorunmez()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var other = await _world.CreateActiveAsync(UserRole.Mentor);
        var task = await _world.AssignTaskAsync(mentor, intern);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.DeleteAsync($"/api/tasks/{task.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await mentor.Client.DeleteAsync($"/api/tasks/{task.Id}")).StatusCode);
        Assert.DoesNotContain((await MineAsync(intern)).Items, t => t.Id == task.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.PostAsync($"/api/tasks/{task.Id}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await mentor.Client.PostAsync($"/api/tasks/{task.Id}/restore", null)).StatusCode);
        Assert.Contains((await MineAsync(intern)).Items, t => t.Id == task.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await mentor.Client.PostAsync($"/api/tasks/{task.Id}/restore", null)).StatusCode);
    }

    [Fact]
    public async Task Listele_DurumFiltresiSunucudaUygulanir_SayfalamaToplamlariDogru_GecersizDurum400()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var tasks = new List<TaskDto>();
        for (int i = 1; i <= 12; i++) tasks.Add(await _world.AssignTaskAsync(mentor, intern, $"Görev {i:00}"));
        foreach (var t in tasks.Take(2)) await SetStatusAsync(intern, t.Id, "InProgress");
        await SetStatusAsync(intern, tasks[2].Id, "Completed");

        var todoPage1 = await MineAsync(intern, "?status=Todo&page=1&pageSize=5");
        var todoPage2 = await MineAsync(intern, "?status=todo&page=2&pageSize=5"); // küçük harf de kabul
        var all = await MineAsync(intern, "?pageSize=1");

        Assert.Equal(9, todoPage1.TotalCount);
        Assert.Equal(2, todoPage1.TotalPages);
        Assert.Equal(5, todoPage1.Items.Count);
        Assert.Equal(4, todoPage2.Items.Count);
        Assert.All(todoPage1.Items.Concat(todoPage2.Items), t => Assert.Equal("Todo", t.Status));
        Assert.Equal(12, all.TotalCount);
        Assert.Equal(2, (await MineAsync(intern, "?status=InProgress")).TotalCount);
        Assert.Equal(HttpStatusCode.BadRequest, (await intern.Client.GetAsync("/api/tasks/mine?status=Bitmis")).StatusCode);
    }

    [Fact]
    public async Task Ozet_DurumSayilariVeGecikenSayisi_TamamlananGecikmisSayilmaz()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var overdueTodo = await _world.AssignTaskAsync(mentor, intern, "Gecikmiş 1", DateTime.UtcNow.Date.AddDays(-3));
        var overdueProgress = await _world.AssignTaskAsync(mentor, intern, "Gecikmiş 2", DateTime.UtcNow.Date.AddDays(-1));
        var overdueDone = await _world.AssignTaskAsync(mentor, intern, "Gecikmiş ama bitti", DateTime.UtcNow.Date.AddDays(-5));
        await _world.AssignTaskAsync(mentor, intern, "Gelecek", DateTime.UtcNow.Date.AddDays(7));
        await _world.AssignTaskAsync(mentor, intern, "Tarihsiz");
        await SetStatusAsync(intern, overdueProgress.Id, "InProgress");
        await SetStatusAsync(intern, overdueDone.Id, "Completed");

        var summary = await TestWorld.ReadAsync<TaskSummaryDto>(await intern.Client.GetAsync("/api/tasks/mine/summary"));

        Assert.Equal(5, summary.TotalTasks);
        Assert.Equal(3, summary.TodoCount);
        Assert.Equal(1, summary.InProgressCount);
        Assert.Equal(1, summary.CompletedCount);
        Assert.Equal(2, summary.OverdueCount); // tamamlanan gecikmiş sayılmaz
        Assert.NotNull(overdueTodo);
    }

    [Fact]
    public async Task Yaklasan_BitisTarihineGoreSirali_TarihsizSonda_TamamlananYok_AdetSinirli()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        await _world.AssignTaskAsync(mentor, intern, "Tarihsiz");
        await _world.AssignTaskAsync(mentor, intern, "Uzak", DateTime.UtcNow.Date.AddDays(20));
        await _world.AssignTaskAsync(mentor, intern, "Gecikmiş", DateTime.UtcNow.Date.AddDays(-2));
        var done = await _world.AssignTaskAsync(mentor, intern, "Bitmiş", DateTime.UtcNow.Date.AddDays(1));
        await _world.AssignTaskAsync(mentor, intern, "Yakın", DateTime.UtcNow.Date.AddDays(2));
        await SetStatusAsync(intern, done.Id, "Completed");

        var upcoming = await TestWorld.ReadAsync<List<TaskDto>>(await intern.Client.GetAsync("/api/tasks/mine/upcoming?take=3"));

        Assert.Equal(new[] { "Gecikmiş", "Yakın", "Uzak" }, upcoming.Select(t => t.Title));
        var capped = await TestWorld.ReadAsync<List<TaskDto>>(await intern.Client.GetAsync("/api/tasks/mine/upcoming?take=500"));
        Assert.Equal(4, capped.Count); // tamamlanan hariç 4 görev var (üst sınır 20)
        Assert.Equal("Tarihsiz", capped.Last().Title);
    }

    [Fact]
    public async Task AtadigimGorevler_YalnizcaKendiAttiklari_AramaBaslikAciklamaVeStajyerAdinaBakar()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var (otherMentor, otherIntern, _) = await _world.MentorWithInternAsync();
        await _world.AssignTaskAsync(mentor, intern, "Swagger dokümantasyonu");
        await _world.AssignTaskAsync(mentor, intern, "Birim testleri");
        await _world.AssignTaskAsync(otherMentor, otherIntern, "Swagger başka mentor");

        var all = await TestWorld.ReadAsync<PagedResultDto<TaskDto>>(await mentor.Client.GetAsync("/api/tasks/created"));
        var byTitle = await TestWorld.ReadAsync<PagedResultDto<TaskDto>>(await mentor.Client.GetAsync("/api/tasks/created?search=swagger"));
        var byAssignee = await TestWorld.ReadAsync<PagedResultDto<TaskDto>>(await mentor.Client.GetAsync($"/api/tasks/created?search={Uri.EscapeDataString(intern.FullName)}"));

        Assert.Equal(2, all.TotalCount); // diğer mentorun görevi sızmaz
        Assert.Equal("Swagger dokümantasyonu", Assert.Single(byTitle.Items).Title);
        Assert.Equal(2, byAssignee.TotalCount);
    }

    [Fact]
    public async Task MentorOzeti_YalnizcaKendiGrubundakiStajyerIcin_YabanciStajyer403()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var foreign = await _world.CreateActiveAsync(UserRole.Intern);
        await _world.AssignTaskAsync(mentor, intern);

        var own = await TestWorld.ReadAsync<TaskSummaryDto>(await mentor.Client.GetAsync($"/api/tasks/summary/{intern.Id}"));
        Assert.Equal(1, own.TotalTasks);
        Assert.Equal(HttpStatusCode.Forbidden, (await mentor.Client.GetAsync($"/api/tasks/summary/{foreign.Id}")).StatusCode);
    }

    [Fact]
    public async Task AdminListeleri_TumGorevlerVeAdlarla_AramaStajyerVeMentorAdinaBakar()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        await _world.AssignTaskAsync(mentor, intern, "Admin görecek");

        var byAssignee = await TestWorld.ReadAsync<PagedResultDto<TaskDto>>(await admin.Client.GetAsync($"/api/admin/tasks?search={Uri.EscapeDataString(intern.Email)}"));
        var byCreator = await TestWorld.ReadAsync<PagedResultDto<TaskDto>>(await admin.Client.GetAsync($"/api/admin/tasks?search={Uri.EscapeDataString(mentor.Email)}"));

        var task = Assert.Single(byAssignee.Items);
        Assert.Equal(intern.FullName, task.AssignedUserName);
        Assert.Equal(mentor.FullName, task.CreatedByUserName);
        Assert.Contains(byCreator.Items, t => t.Title == "Admin görecek");
    }
}
