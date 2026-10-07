using System.Net;
using System.Net.Http.Json;
using Backend.Dtos;
using Backend.Entities;

namespace Backend.IntegrationTests;

// Staj defteri: günde tek kayıt, onay akışı (taslak -> gönderildi -> onaylı/düzeltme), mentor yetkisi, dışa aktarma.
[Collection("api")]
public class JournalTests
{
    private readonly TestWorld _world;

    public JournalTests(ApiFactory factory)
    {
        _world = new TestWorld(factory);
    }

    private static DateTime Day(int daysAgo) => DateTime.UtcNow.Date.AddDays(-daysAgo);

    private async Task<InternshipNoteDto> CreateAsync(TestUser intern, int daysAgo = 1, string title = "Defter kaydı") =>
        await TestWorld.ReadAsync<InternshipNoteDto>(await intern.Client.PostAsJsonAsync("/api/notes", TestWorld.Entry(Day(daysAgo), title)), HttpStatusCode.Created);

    private static Task<HttpResponseMessage> SubmitAsync(TestUser intern, int id) => intern.Client.PostAsync($"/api/notes/{id}/submit", null);

    private static Task<HttpResponseMessage> ReviewAsync(TestUser mentor, int id, bool approve, string? comment = null) =>
        mentor.Client.PostAsJsonAsync($"/api/notes/{id}/review", new ReviewNoteRequestDto { Approve = approve, Comment = comment });

    [Fact]
    public async Task Olustur_TaslakBaslar_EtiketlerTemizlenir_TarihGeceYarisinaCekilir_YazarAdiDoner()
    {
        var intern = await _world.CreateActiveAsync(UserRole.Intern, "Stajyer Elif");
        var request = TestWorld.Entry(Day(2).AddHours(15), "  İlk gün  ");
        request.Tags = " ef core ,JWT,, EF Core ";

        var note = await TestWorld.ReadAsync<InternshipNoteDto>(await intern.Client.PostAsJsonAsync("/api/notes", request), HttpStatusCode.Created);

        Assert.Equal("Draft", note.Status);
        Assert.Equal("İlk gün", note.Title);
        Assert.Equal("ef core, JWT", note.Tags);
        Assert.Equal(Day(2), note.NoteDate);
        Assert.Equal(6.5m, note.HoursSpent);
        Assert.Equal("Stajyer Elif", note.UserName);
    }

    [Fact]
    public async Task Olustur_AyniGuneIkinciKayit409_GelecekTarih400_SinirDisiSure400_Dogrulama400()
    {
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        await CreateAsync(intern, daysAgo: 3);

        Assert.Equal(HttpStatusCode.Conflict, (await intern.Client.PostAsJsonAsync("/api/notes", TestWorld.Entry(Day(3).AddHours(9)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await intern.Client.PostAsJsonAsync("/api/notes", TestWorld.Entry(DateTime.UtcNow.Date.AddDays(10)))).StatusCode);

        var tooLong = TestWorld.Entry(Day(10));
        tooLong.HoursSpent = 30m;
        Assert.Equal(HttpStatusCode.BadRequest, (await intern.Client.PostAsJsonAsync("/api/notes", tooLong)).StatusCode);

        var noHours = TestWorld.Entry(Day(11));
        noHours.HoursSpent = 0m;
        Assert.Equal(HttpStatusCode.BadRequest, (await intern.Client.PostAsJsonAsync("/api/notes", noHours)).StatusCode);

        foreach (var title in new[] { "", "x", new string('x', 151) })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await intern.Client.PostAsJsonAsync("/api/notes", TestWorld.Entry(Day(12), title))).StatusCode);
        }

        var longContent = TestWorld.Entry(Day(13));
        longContent.Content = new string('x', 4001);
        Assert.Equal(HttpStatusCode.BadRequest, (await intern.Client.PostAsJsonAsync("/api/notes", longContent)).StatusCode);
    }

    [Fact]
    public async Task OndalikliSure_TurkceKulturdeDe500VermezKaydedilir()
    {
        // Regresyon: [Range(typeof(decimal), "0.25", ...)] sunucu kültürüne bağlıydı ve süre girilen her kayıtta 500 veriyordu.
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        var request = TestWorld.Entry(Day(1));
        request.HoursSpent = 0.25m;

        var note = await TestWorld.ReadAsync<InternshipNoteDto>(await intern.Client.PostAsJsonAsync("/api/notes", request), HttpStatusCode.Created);

        Assert.Equal(0.25m, note.HoursSpent);
    }

    [Fact]
    public async Task Taslak_MentoraGorunmez_GonderilinceKuyrugaDuser_BaskaMentorGormez()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var otherMentor = await _world.CreateActiveAsync(UserRole.Mentor);
        var note = await CreateAsync(intern, title: "Gizli taslak başlığı");

        async Task<int> QueueSize(TestUser m) =>
            (await TestWorld.ReadAsync<PagedResultDto<InternshipNoteDto>>(await m.Client.GetAsync("/api/notes/review?search=Gizli+taslak"))).TotalCount;

        Assert.Equal(0, await QueueSize(mentor));
        Assert.Equal(HttpStatusCode.Forbidden, (await mentor.Client.GetAsync($"/api/notes/{note.Id}")).StatusCode); // taslağı tekil de göremez

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(intern, note.Id)).StatusCode);

        Assert.Equal(1, await QueueSize(mentor));
        Assert.Equal(0, await QueueSize(otherMentor)); // başka mentorun kuyruğunda sızıntı yok
        Assert.Equal(HttpStatusCode.OK, (await mentor.Client.GetAsync($"/api/notes/{note.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherMentor.Client.GetAsync($"/api/notes/{note.Id}")).StatusCode);
    }

    [Fact]
    public async Task Gonderilmis_DuzenlenemezVeSilinemez409_GeriCekilinceTekrarDuzenlenir()
    {
        var (_, intern, _) = await _world.MentorWithInternAsync();
        var note = await CreateAsync(intern);
        await SubmitAsync(intern, note.Id);

        var update = new UpdateNoteRequestDto { Title = "Yeni", Content = "Yeni içerik", NoteDate = Day(1) };
        Assert.Equal(HttpStatusCode.Conflict, (await intern.Client.PutAsJsonAsync($"/api/notes/{note.Id}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await intern.Client.DeleteAsync($"/api/notes/{note.Id}")).StatusCode);

        var withdrawn = await TestWorld.ReadAsync<InternshipNoteDto>(await intern.Client.PostAsync($"/api/notes/{note.Id}/withdraw", null));
        Assert.Equal("Draft", withdrawn.Status);
        var edited = await TestWorld.ReadAsync<InternshipNoteDto>(await intern.Client.PutAsJsonAsync($"/api/notes/{note.Id}", update));
        Assert.Equal("Yeni", edited.Title);
        Assert.Equal(HttpStatusCode.Conflict, (await intern.Client.PostAsync($"/api/notes/{note.Id}/withdraw", null)).StatusCode); // taslak geri çekilemez
    }

    [Fact]
    public async Task MentorOnaylar_KayitKilitlenir_DuzenleSilGeriCekGonder_Hepsi409()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var note = await CreateAsync(intern);
        await SubmitAsync(intern, note.Id);

        var approved = await TestWorld.ReadAsync<InternshipNoteDto>(await ReviewAsync(mentor, note.Id, approve: true, comment: " Güzel iş "));

        Assert.Equal("Approved", approved.Status);
        Assert.Equal("Güzel iş", approved.MentorComment);
        Assert.Equal(mentor.FullName, approved.ReviewedByName);
        Assert.NotNull(approved.ReviewedAt);
        var update = new UpdateNoteRequestDto { Title = "Hile", Content = "x", NoteDate = Day(1) };
        Assert.Equal(HttpStatusCode.Conflict, (await intern.Client.PutAsJsonAsync($"/api/notes/{note.Id}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await intern.Client.DeleteAsync($"/api/notes/{note.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await intern.Client.PostAsync($"/api/notes/{note.Id}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SubmitAsync(intern, note.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ReviewAsync(mentor, note.Id, approve: false, comment: "Vazgeçtim")).StatusCode); // iki kez değerlendirilemez
    }

    [Fact]
    public async Task DuzeltmeIstegi_AciklamaZorunlu_StajyerDuzeltipTekrarGonderir_SonraOnaylanir()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var note = await CreateAsync(intern);
        await SubmitAsync(intern, note.Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await ReviewAsync(mentor, note.Id, approve: false, comment: "   ")).StatusCode);

        var returned = await TestWorld.ReadAsync<InternshipNoteDto>(await ReviewAsync(mentor, note.Id, approve: false, comment: "Süreyi ekle"));
        Assert.Equal("ReturnedForRevision", returned.Status);
        Assert.Equal("Süreyi ekle", returned.MentorComment);

        // Düzeltme istenen kayıt düzenlenir, tekrar gönderilir, mentor onaylar.
        var edit = new UpdateNoteRequestDto { Title = "Düzeltildi", Content = "Süreyle", HoursSpent = 8m, NoteDate = Day(1) };
        Assert.Equal(HttpStatusCode.OK, (await intern.Client.PutAsJsonAsync($"/api/notes/{note.Id}", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(intern, note.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ReviewAsync(mentor, note.Id, approve: true)).StatusCode);
    }

    [Fact]
    public async Task Degerlendirme_YalnizcaKendiStajyerininKaydi_BaskaMentorVeStajyer403()
    {
        var (mentor, intern, _) = await _world.MentorWithInternAsync();
        var otherMentor = await _world.CreateActiveAsync(UserRole.Mentor);
        var note = await CreateAsync(intern);
        await SubmitAsync(intern, note.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await ReviewAsync(otherMentor, note.Id, approve: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ReviewAsync(intern, note.Id, approve: true)).StatusCode); // mentor rolü yok
        Assert.Equal(HttpStatusCode.NotFound, (await ReviewAsync(mentor, 999999, approve: true)).StatusCode);
        // Kayıt hâlâ "gönderildi": yetkisiz denemeler durumu değiştirmedi.
        var current = await TestWorld.ReadAsync<InternshipNoteDto>(await intern.Client.GetAsync($"/api/notes/{note.Id}"));
        Assert.Equal("Submitted", current.Status);
    }

    [Fact]
    public async Task BaskasininKaydi_GorulemezDegistirilemezSilinemezGonderilemez_403()
    {
        var (_, intern, _) = await _world.MentorWithInternAsync();
        var stranger = await _world.CreateActiveAsync(UserRole.Intern);
        var note = await CreateAsync(intern);
        var update = new UpdateNoteRequestDto { Title = "Hile", Content = "x", NoteDate = Day(1) };

        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.Client.GetAsync($"/api/notes/{note.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.Client.PutAsJsonAsync($"/api/notes/{note.Id}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.Client.DeleteAsync($"/api/notes/{note.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SubmitAsync(stranger, note.Id)).StatusCode);
    }

    [Fact]
    public async Task EskiBaslıksizNot_MentoraGonderilemez400_BaslikEklenince_Gonderilir()
    {
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        // Eski sürümden kalan, başlığı olmayan not (migration sonrası böyle kayıtlar var).
        int legacyId = await _world.WithDbAsync(async db =>
        {
            var legacy = new InternshipNote { UserId = intern.Id, Content = "eski not", NoteDate = Day(30), Title = string.Empty };
            db.InternshipNotes.Add(legacy);
            await db.SaveChangesAsync();
            return legacy.Id;
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await SubmitAsync(intern, legacyId)).StatusCode);

        var fix = new UpdateNoteRequestDto { Title = "Başlık eklendi", Content = "eski not", NoteDate = Day(30) };
        Assert.Equal(HttpStatusCode.OK, (await intern.Client.PutAsJsonAsync($"/api/notes/{legacyId}", fix)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(intern, legacyId)).StatusCode);
    }

    [Fact]
    public async Task TarihDegisince_BaskaKayitliGuneTasinamaz409()
    {
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        var first = await CreateAsync(intern, daysAgo: 4);
        await CreateAsync(intern, daysAgo: 5);

        var update = new UpdateNoteRequestDto { Title = "Taşı", Content = "x", NoteDate = Day(5) };

        Assert.Equal(HttpStatusCode.Conflict, (await intern.Client.PutAsJsonAsync($"/api/notes/{first.Id}", update)).StatusCode);
    }

    [Fact]
    public async Task SilVeGeriGetir_TaslakSilinebilir_AyniGuneYeniKayitAcildiysaGeriGetirme409()
    {
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        var note = await CreateAsync(intern, daysAgo: 6);

        Assert.Equal(HttpStatusCode.NoContent, (await intern.Client.DeleteAsync($"/api/notes/{note.Id}")).StatusCode);
        var list = await TestWorld.ReadAsync<PagedResultDto<InternshipNoteDto>>(await intern.Client.GetAsync("/api/notes/mine"));
        Assert.DoesNotContain(list.Items, n => n.Id == note.Id);

        Assert.Equal(HttpStatusCode.OK, (await intern.Client.PostAsync($"/api/notes/{note.Id}/restore", null)).StatusCode);

        // Sil, o güne yeni kayıt aç, eskisini geri getirmeye çalış: iki kayıt çakışmasın.
        await intern.Client.DeleteAsync($"/api/notes/{note.Id}");
        await CreateAsync(intern, daysAgo: 6, title: "Yerine açılan kayıt");
        Assert.Equal(HttpStatusCode.Conflict, (await intern.Client.PostAsync($"/api/notes/{note.Id}/restore", null)).StatusCode);
    }

    [Fact]
    public async Task Liste_TariheGoreEnYeniOnce_DurumVeAramaSunucudaSuzulur_GecersizDurum400()
    {
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        var oldest = await CreateAsync(intern, daysAgo: 9, title: "En eski kayıt");
        var middle = await CreateAsync(intern, daysAgo: 8, title: "Orta kayıt zeytin");
        var newest = await CreateAsync(intern, daysAgo: 7, title: "En yeni kayıt");
        await SubmitAsync(intern, middle.Id);

        var all = await TestWorld.ReadAsync<PagedResultDto<InternshipNoteDto>>(await intern.Client.GetAsync("/api/notes/mine"));
        var submitted = await TestWorld.ReadAsync<PagedResultDto<InternshipNoteDto>>(await intern.Client.GetAsync("/api/notes/mine?status=Submitted"));
        var search = await TestWorld.ReadAsync<PagedResultDto<InternshipNoteDto>>(await intern.Client.GetAsync("/api/notes/mine?search=zeytin"));
        var tagSearch = await TestWorld.ReadAsync<PagedResultDto<InternshipNoteDto>>(await intern.Client.GetAsync("/api/notes/mine?search=ef+core"));

        Assert.Equal(new[] { newest.Id, middle.Id, oldest.Id }, all.Items.Select(n => n.Id));
        Assert.Equal(middle.Id, Assert.Single(submitted.Items).Id);
        Assert.Equal(middle.Id, Assert.Single(search.Items).Id);
        Assert.Equal(3, tagSearch.TotalCount); // etikette de aranır
        Assert.Equal(HttpStatusCode.BadRequest, (await intern.Client.GetAsync("/api/notes/mine?status=Bilinmiyor")).StatusCode);
    }

    [Fact]
    public async Task MentorKuyrugu_DurumuVeStajyerAdiylaAramaSuzer_TaslakFiltresi400()
    {
        var (mentor, intern, group) = await _world.MentorWithInternAsync();
        var second = await _world.CreateActiveAsync(UserRole.Intern, "Stajyer Ceren");
        await _world.AddMemberAsync(mentor, group.Id, second);
        var a = await CreateAsync(intern, daysAgo: 2);
        var b = await CreateAsync(second, daysAgo: 2);
        await SubmitAsync(intern, a.Id);
        await SubmitAsync(second, b.Id);
        await ReviewAsync(mentor, a.Id, approve: true);

        var pending = await TestWorld.ReadAsync<PagedResultDto<InternshipNoteDto>>(await mentor.Client.GetAsync("/api/notes/review?status=Submitted"));
        var byName = await TestWorld.ReadAsync<PagedResultDto<InternshipNoteDto>>(await mentor.Client.GetAsync("/api/notes/review?search=Ceren"));

        Assert.Equal(b.Id, Assert.Single(pending.Items).Id);
        Assert.Equal(b.Id, Assert.Single(byName.Items).Id);
        Assert.Equal("Stajyer Ceren", byName.Items[0].UserName);
        Assert.Equal(HttpStatusCode.BadRequest, (await mentor.Client.GetAsync("/api/notes/review?status=Draft")).StatusCode);
    }

    [Fact]
    public async Task DisaAktar_EskidenYeniyeSirali_TarihAraligiFiltrelenir_TersAralik400_YalnizcaKendiKayitlari()
    {
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        var other = await _world.CreateActiveAsync(UserRole.Intern);
        var d1 = await CreateAsync(intern, daysAgo: 9, title: "1. gün");
        var d2 = await CreateAsync(intern, daysAgo: 8, title: "2. gün");
        var d3 = await CreateAsync(intern, daysAgo: 7, title: "3. gün");
        await CreateAsync(other, daysAgo: 8, title: "Başkasının kaydı");

        var all = await TestWorld.ReadAsync<List<InternshipNoteDto>>(await intern.Client.GetAsync("/api/notes/export"));
        string from = Day(8).ToString("yyyy-MM-dd"), to = Day(8).ToString("yyyy-MM-dd");
        var one = await TestWorld.ReadAsync<List<InternshipNoteDto>>(await intern.Client.GetAsync($"/api/notes/export?from={from}&to={to}"));

        Assert.Equal(new[] { d1.Id, d2.Id, d3.Id }, all.Select(n => n.Id)); // eskiden yeniye, başkasının kaydı yok
        Assert.Equal(d2.Id, Assert.Single(one).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await intern.Client.GetAsync($"/api/notes/export?from={Day(1):yyyy-MM-dd}&to={Day(5):yyyy-MM-dd}")).StatusCode);
    }

    [Fact]
    public async Task Sayfalama_OnarliSayfalar_ToplamSayfaSayisiDogru()
    {
        var intern = await _world.CreateActiveAsync(UserRole.Intern);
        for (int i = 1; i <= 12; i++) await CreateAsync(intern, daysAgo: 20 + i, title: $"Kayıt {i:00}");

        var page1 = await TestWorld.ReadAsync<PagedResultDto<InternshipNoteDto>>(await intern.Client.GetAsync("/api/notes/mine?page=1&pageSize=10"));
        var page2 = await TestWorld.ReadAsync<PagedResultDto<InternshipNoteDto>>(await intern.Client.GetAsync("/api/notes/mine?page=2&pageSize=10"));

        Assert.Equal(12, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Empty(page1.Items.Select(n => n.Id).Intersect(page2.Items.Select(n => n.Id))); // sayfalar çakışmaz
    }
}
