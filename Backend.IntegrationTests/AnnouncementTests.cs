using System.Net;
using System.Net.Http.Json;
using Backend.Dtos;
using Backend.Entities;

namespace Backend.IntegrationTests;

// Grup duyuruları: yalnızca grubun mentoru yazar/siler; grubun üyeleri okur; başka gruba sızmaz.
[Collection("api")]
public class AnnouncementTests
{
    private readonly TestWorld _world;

    public AnnouncementTests(ApiFactory factory)
    {
        _world = new TestWorld(factory);
    }

    private static Task<HttpResponseMessage> PostAsync(TestUser user, int groupId, string title = "Cuma sunumu", string content = "Saat 14:00") =>
        user.Client.PostAsJsonAsync($"/api/groups/{groupId}/announcements", new CreateAnnouncementRequestDto { Title = title, Content = content });

    [Fact]
    public async Task Mentor_GrubunaDuyuruYazar_AdlarlaDoner()
    {
        var (mentor, _, group) = await _world.MentorWithInternAsync();

        var created = await TestWorld.ReadAsync<AnnouncementDto>(await PostAsync(mentor, group.Id, "  Başlık  ", "  İçerik  "), HttpStatusCode.Created);

        Assert.Equal("Başlık", created.Title);
        Assert.Equal("İçerik", created.Content);
        Assert.Equal(group.Name, created.GroupName);
        Assert.Equal(mentor.FullName, created.MentorName);
    }

    [Fact]
    public async Task Yazma_StajyerBaskaMentorVeAdmin403_Dogrulama400_OlmayanGrup404()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();
        var otherMentor = await _world.CreateActiveAsync(UserRole.Mentor);
        var admin = await _world.CreateActiveAsync(UserRole.Admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(intern, group.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(otherMentor, group.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(admin, group.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(mentor, 999999)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(mentor, group.Id, title: "x")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(mentor, group.Id, title: new string('x', 151))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(mentor, group.Id, content: new string('x', 2001))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(mentor, group.Id, content: "")).StatusCode);
    }

    [Fact]
    public async Task Okuma_GrubunMentoruVeUyeleriGorur_YabanciStajyer403()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();
        var stranger = await _world.CreateActiveAsync(UserRole.Intern);
        await PostAsync(mentor, group.Id);

        var asMember = await TestWorld.ReadAsync<PagedResultDto<AnnouncementDto>>(await intern.Client.GetAsync($"/api/groups/{group.Id}/announcements"));
        var asMentor = await TestWorld.ReadAsync<PagedResultDto<AnnouncementDto>>(await mentor.Client.GetAsync($"/api/groups/{group.Id}/announcements"));

        Assert.Single(asMember.Items);
        Assert.Single(asMentor.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.Client.GetAsync($"/api/groups/{group.Id}/announcements")).StatusCode);
    }

    [Fact]
    public async Task Stajyerin_DuyuruAkisi_YalnizcaUyesiOlduguGruplar_EnYeniOnce_SilinenVeGrupDisiGorunmez()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();
        var (otherMentor, otherIntern, otherGroup) = await _world.MentorWithInternAsync();
        await PostAsync(mentor, group.Id, "Eski duyuru");
        await Task.Delay(30); // aynı milisaniyede oluşmasın: sıralama CreatedAt'e göre
        var newer = await TestWorld.ReadAsync<AnnouncementDto>(await PostAsync(mentor, group.Id, "Yeni duyuru"), HttpStatusCode.Created);
        var deleted = await TestWorld.ReadAsync<AnnouncementDto>(await PostAsync(mentor, group.Id, "Silinecek"), HttpStatusCode.Created);
        await PostAsync(otherMentor, otherGroup.Id, "Başka grubun duyurusu");
        await mentor.Client.DeleteAsync($"/api/groups/{group.Id}/announcements/{deleted.Id}");

        var feed = await TestWorld.ReadAsync<PagedResultDto<AnnouncementDto>>(await intern.Client.GetAsync("/api/announcements/mine"));
        var otherFeed = await TestWorld.ReadAsync<PagedResultDto<AnnouncementDto>>(await otherIntern.Client.GetAsync("/api/announcements/mine"));

        Assert.Equal(new[] { "Yeni duyuru", "Eski duyuru" }, feed.Items.Select(a => a.Title)); // silinen ve başka grup yok
        Assert.Equal(newer.Id, feed.Items[0].Id);
        Assert.Equal(group.Name, feed.Items[0].GroupName);
        Assert.Equal(mentor.FullName, feed.Items[0].MentorName);
        Assert.Equal("Başka grubun duyurusu", Assert.Single(otherFeed.Items).Title);
    }

    [Fact]
    public async Task Silme_SadeceGrubunMentoru_BaskaGrubunAdresiyleSilinemez404_StajyerSilemez403()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();
        var secondGroup = await _world.CreateGroupAsync(mentor);
        var otherMentor = await _world.CreateActiveAsync(UserRole.Mentor);
        var announcement = await TestWorld.ReadAsync<AnnouncementDto>(await PostAsync(mentor, secondGroup.Id), HttpStatusCode.Created);

        Assert.Equal(HttpStatusCode.Forbidden, (await intern.Client.DeleteAsync($"/api/groups/{group.Id}/announcements/{announcement.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherMentor.Client.DeleteAsync($"/api/groups/{secondGroup.Id}/announcements/{announcement.Id}")).StatusCode);
        // Duyuru 2. gruba ait; 1. grubun adresiyle (kendi grubu olsa bile) silinemez.
        Assert.Equal(HttpStatusCode.NotFound, (await mentor.Client.DeleteAsync($"/api/groups/{group.Id}/announcements/{announcement.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await mentor.Client.DeleteAsync($"/api/groups/{secondGroup.Id}/announcements/{announcement.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mentor.Client.DeleteAsync($"/api/groups/{secondGroup.Id}/announcements/{announcement.Id}")).StatusCode);
    }

    [Fact]
    public async Task SilinenGrubunDuyurulari_StajyerAkisindaGorunmez()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();
        await PostAsync(mentor, group.Id, "Grup silinince kaybolmalı");
        await mentor.Client.DeleteAsync($"/api/groups/{group.Id}");

        var feed = await TestWorld.ReadAsync<PagedResultDto<AnnouncementDto>>(await intern.Client.GetAsync("/api/announcements/mine"));

        Assert.Empty(feed.Items);
    }

    [Fact]
    public async Task Sayfalama_BesliSayfalarVeToplam()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();
        for (int i = 1; i <= 7; i++) await PostAsync(mentor, group.Id, $"Duyuru {i}");

        var page1 = await TestWorld.ReadAsync<PagedResultDto<AnnouncementDto>>(await intern.Client.GetAsync("/api/announcements/mine?page=1&pageSize=5"));
        var page2 = await TestWorld.ReadAsync<PagedResultDto<AnnouncementDto>>(await intern.Client.GetAsync("/api/announcements/mine?page=2&pageSize=5"));

        Assert.Equal(7, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(5, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
    }
}
