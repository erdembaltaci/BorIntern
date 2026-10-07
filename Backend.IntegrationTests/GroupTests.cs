using System.Net;
using System.Net.Http.Json;
using Backend.Dtos;
using Backend.Entities;

namespace Backend.IntegrationTests;

// Grup yönetimi, üyelik kuralları ve mentor sahipliği.
[Collection("api")]
public class GroupTests
{
    private readonly TestWorld _world;

    public GroupTests(ApiFactory factory)
    {
        _world = new TestWorld(factory);
    }

    [Fact]
    public async Task Olustur_ListedeGorunur_AyniAdIkinciKezKullanilamaz409()
    {
        var mentor = await _world.CreateActiveAsync(UserRole.Mentor);
        var group = await _world.CreateGroupAsync(mentor, "Yaz Stajı Ekibi");

        var duplicate = await mentor.Client.PostAsJsonAsync("/api/groups", new CreateGroupRequestDto { Name = "Yaz Stajı Ekibi" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var mine = await TestWorld.ReadAsync<PagedResultDto<GroupDto>>(await mentor.Client.GetAsync("/api/groups/mine"));
        Assert.Contains(mine.Items, g => g.Id == group.Id);
        Assert.Equal(mentor.Id, group.MentorId);
        Assert.Equal(mentor.FullName, group.MentorName); // ad, toplu sorguyla DTO'da gelir
    }

    [Fact]
    public async Task Olustur_KisaVeyaBosAd_400()
    {
        var mentor = await _world.CreateActiveAsync(UserRole.Mentor);

        foreach (var name in new[] { "", "a" })
        {
            var response = await mentor.Client.PostAsJsonAsync("/api/groups", new CreateGroupRequestDto { Name = name });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Stajyer_GrupOlusturamaz_403()
    {
        var intern = await _world.CreateActiveAsync(UserRole.Intern);

        var response = await intern.Client.PostAsJsonAsync("/api/groups", new CreateGroupRequestDto { Name = "Hile Grubu" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task YenidenAdlandir_DigerGrubunAdinaCakisirsa409_KendiAdiylaAyniysaSorunYok()
    {
        var mentor = await _world.CreateActiveAsync(UserRole.Mentor);
        var a = await _world.CreateGroupAsync(mentor, "Grup A " + Guid.NewGuid().ToString("N")[..6]);
        var b = await _world.CreateGroupAsync(mentor, "Grup B " + Guid.NewGuid().ToString("N")[..6]);

        var clash = await mentor.Client.PutAsJsonAsync($"/api/groups/{b.Id}", new CreateGroupRequestDto { Name = a.Name });
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);

        var same = await mentor.Client.PutAsJsonAsync($"/api/groups/{b.Id}", new CreateGroupRequestDto { Name = b.Name });
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);

        var renamed = await mentor.Client.PutAsJsonAsync($"/api/groups/{b.Id}", new CreateGroupRequestDto { Name = "Yeni Ad " + Guid.NewGuid().ToString("N")[..6] });
        Assert.StartsWith("Yeni Ad", (await TestWorld.ReadAsync<GroupDto>(renamed)).Name);
    }

    [Fact]
    public async Task BaskaMentor_GrubaDokunamaz_403_OlmayanGrup404()
    {
        var owner = await _world.CreateActiveAsync(UserRole.Mentor);
        var stranger = await _world.CreateActiveAsync(UserRole.Mentor);
        var group = await _world.CreateGroupAsync(owner);
        var intern = await _world.CreateActiveAsync(UserRole.Intern);

        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.Client.GetAsync($"/api/groups/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.Client.PutAsJsonAsync($"/api/groups/{group.Id}", new CreateGroupRequestDto { Name = "Çalındı" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.Client.DeleteAsync($"/api/groups/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.Client.PostAsJsonAsync($"/api/groups/{group.Id}/members", new AddGroupMemberRequestDto { UserId = intern.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.GetAsync("/api/groups/999999")).StatusCode);
    }

    [Fact]
    public async Task Sil_ListedenVeTekilGorunumdenKalkar_GeriGetirinceDonerVeZatenSilinmemisseHata()
    {
        var mentor = await _world.CreateActiveAsync(UserRole.Mentor);
        var group = await _world.CreateGroupAsync(mentor);

        Assert.Equal(HttpStatusCode.NoContent, (await mentor.Client.DeleteAsync($"/api/groups/{group.Id}")).StatusCode);
        var mine = await TestWorld.ReadAsync<PagedResultDto<GroupDto>>(await mentor.Client.GetAsync("/api/groups/mine"));
        Assert.DoesNotContain(mine.Items, g => g.Id == group.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await mentor.Client.GetAsync($"/api/groups/{group.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await mentor.Client.PostAsync($"/api/groups/{group.Id}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await mentor.Client.GetAsync($"/api/groups/{group.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await mentor.Client.PostAsync($"/api/groups/{group.Id}/restore", null)).StatusCode);
    }

    [Fact]
    public async Task UyeEkle_YalnizcaAktifStajyer_OlmayanKullanici404_Mentor400_Bekleyen400_Tekrar409()
    {
        var mentor = await _world.CreateActiveAsync(UserRole.Mentor);
        var group = await _world.CreateGroupAsync(mentor);
        var intern = await _world.CreateActiveAsync(UserRole.Intern, "Stajyer Elif");
        var otherMentor = await _world.CreateActiveAsync(UserRole.Mentor);
        var pending = await _world.RegisterAsync("Bekleyen");
        Task<HttpResponseMessage> Add(int userId) =>
            mentor.Client.PostAsJsonAsync($"/api/groups/{group.Id}/members", new AddGroupMemberRequestDto { UserId = userId });

        Assert.Equal(HttpStatusCode.NotFound, (await Add(999999)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Add(otherMentor.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Add(pending.Id)).StatusCode);

        var added = await TestWorld.ReadAsync<GroupMemberDto>(await Add(intern.Id), HttpStatusCode.OK);
        Assert.Equal("Stajyer Elif", added.FullName);
        Assert.Equal(intern.Email, added.Email);
        Assert.Equal(HttpStatusCode.Conflict, (await Add(intern.Id)).StatusCode);
    }

    [Fact]
    public async Task UyeListesi_MentorVeUyeGorur_YabanciStajyer403_AdVeEpostaDoludur()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();
        var stranger = await _world.CreateActiveAsync(UserRole.Intern);

        var asMentor = await TestWorld.ReadAsync<PagedResultDto<GroupMemberDto>>(await mentor.Client.GetAsync($"/api/groups/{group.Id}/members"));
        var asMember = await TestWorld.ReadAsync<PagedResultDto<GroupMemberDto>>(await intern.Client.GetAsync($"/api/groups/{group.Id}/members"));

        Assert.Single(asMentor.Items);
        Assert.Equal(intern.FullName, asMentor.Items[0].FullName);
        Assert.Equal(intern.Email, asMentor.Items[0].Email);
        Assert.Equal(asMentor.Items[0].UserId, asMember.Items[0].UserId);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.Client.GetAsync($"/api/groups/{group.Id}/members")).StatusCode);
    }

    [Fact]
    public async Task UyeCikar_SoftDelete_UyelikBitinceListedenVeStajyerArayisindanDuser()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await mentor.Client.DeleteAsync($"/api/groups/{group.Id}/members/{intern.Id}")).StatusCode);

        var members = await TestWorld.ReadAsync<PagedResultDto<GroupMemberDto>>(await mentor.Client.GetAsync($"/api/groups/{group.Id}/members"));
        Assert.Empty(members.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await mentor.Client.DeleteAsync($"/api/groups/{group.Id}/members/{intern.Id}")).StatusCode);
        // Üyelik bitti: stajyer artık mentorun "kendi stajyerlerim" listesinde yok ve ona görev atanamaz.
        var mine = await TestWorld.ReadAsync<PagedResultDto<UserDto>>(await mentor.Client.GetAsync("/api/users/my-interns"));
        Assert.DoesNotContain(mine.Items, u => u.Id == intern.Id);
        var assign = await mentor.Client.PostAsJsonAsync("/api/tasks", new CreateTaskRequestDto { Title = "Görev", AssignedUserId = intern.Id });
        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
    }

    [Fact]
    public async Task KendiStajyerlerim_YalnizcaKendiGruplarindakiler_BirdenCokGrupta_BirKezGelir()
    {
        var mentor = await _world.CreateActiveAsync(UserRole.Mentor);
        var other = await _world.CreateActiveAsync(UserRole.Mentor);
        var internA = await _world.CreateActiveAsync(UserRole.Intern, "Elif Sarac");
        var internB = await _world.CreateActiveAsync(UserRole.Intern, "Ceren Aksoy");
        var foreign = await _world.CreateActiveAsync(UserRole.Intern, "Yabanci Kisi");
        var g1 = await _world.CreateGroupAsync(mentor);
        var g2 = await _world.CreateGroupAsync(mentor);
        await _world.AddMemberAsync(mentor, g1.Id, internA);
        await _world.AddMemberAsync(mentor, g2.Id, internA); // aynı stajyer iki grupta
        await _world.AddMemberAsync(mentor, g2.Id, internB);
        await _world.AddMemberAsync(other, (await _world.CreateGroupAsync(other)).Id, foreign);

        var result = await TestWorld.ReadAsync<PagedResultDto<UserDto>>(await mentor.Client.GetAsync("/api/users/my-interns"));

        Assert.Equal(new[] { internB.Id, internA.Id }.Order(), result.Items.Select(u => u.Id).Order());
        Assert.Equal(2, result.TotalCount);
        var filtered = await TestWorld.ReadAsync<PagedResultDto<UserDto>>(await mentor.Client.GetAsync("/api/users/my-interns?search=ceren"));
        Assert.Equal(internB.Id, Assert.Single(filtered.Items).Id);
        var leak = await TestWorld.ReadAsync<PagedResultDto<UserDto>>(await mentor.Client.GetAsync("/api/users/my-interns?search=Yabanci"));
        Assert.Empty(leak.Items);
    }

    [Fact]
    public async Task StajyerArama_YalnizcaAktifStajyerler_MentorVeBekleyenGelmez()
    {
        var mentor = await _world.CreateActiveAsync(UserRole.Mentor);
        string marker = Guid.NewGuid().ToString("N")[..8];
        var active = await _world.CreateActiveAsync(UserRole.Intern, $"Aranan {marker}");
        await _world.CreateActiveAsync(UserRole.Mentor, $"Aranan Mentor {marker}");
        await _world.RegisterAsync($"Aranan Bekleyen {marker}");

        var result = await TestWorld.ReadAsync<PagedResultDto<UserDto>>(await mentor.Client.GetAsync($"/api/users/interns?search={marker}"));

        Assert.Equal(active.Id, Assert.Single(result.Items).Id);
    }
}
