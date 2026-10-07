using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;
using Backend.Services;
using Moq;
using Xunit;

namespace Backend.Tests;

// Staj defteri kuralları: günde tek kayıt, durum akışı (taslak -> gönderildi -> onaylı/düzeltme), mentor yetkisi.
public class JournalWorkflowTests
{
    private sealed class Fixture
    {
        public Mock<IInternshipNoteRepository> Notes { get; } = new();
        public Mock<IGroupMemberRepository> Members { get; } = new();
        public Mock<IUserRepository> Users { get; } = new();
        public InternshipNoteService Service { get; }

        public Fixture()
        {
            Users.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<User>());
            Service = new InternshipNoteService(Notes.Object, Members.Object, Users.Object);
        }

        public InternshipNote Existing(NoteStatus status, int userId = 1, string title = "Başlık", string content = "İş")
        {
            var note = new InternshipNote { Id = 10, UserId = userId, Status = status, Title = title, Content = content, NoteDate = DateTime.UtcNow.Date };
            Notes.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(note);
            return note;
        }
    }

    private static CreateNoteRequestDto NewRequest(DateTime? date = null) =>
        new() { Title = "  EF Core  ", Content = "  Migration yazdım  ", Learned = " Index mantığı ", HoursSpent = 6.5m, Tags = "ef core,  JWT ,, EF Core ", NoteDate = date };

    // ---------------------------------------------------------------- oluşturma

    [Fact]
    public async Task Create_AlanlariKirparEtiketleriTemizlerVeGunuGeceYarisinaCeker()
    {
        var f = new Fixture();
        InternshipNote? saved = null;
        f.Notes.Setup(r => r.AddAsync(It.IsAny<InternshipNote>())).Callback<InternshipNote>(n => saved = n).Returns(Task.CompletedTask);

        var day = new DateTime(2026, 3, 4, 15, 30, 0);
        var result = await f.Service.CreateNoteAsync(7, NewRequest(day));

        Assert.NotNull(saved);
        Assert.Equal("EF Core", saved!.Title);
        Assert.Equal("Migration yazdım", saved.Content);
        Assert.Equal("Index mantığı", saved.Learned);
        Assert.Equal("ef core, JWT", saved.Tags); // tekrar ("EF Core") ve boş parçalar atıldı
        Assert.Equal(new DateTime(2026, 3, 4), saved.NoteDate); // saat bilgisi atıldı
        Assert.Equal(NoteStatus.Draft, saved.Status);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(6.5m, result.HoursSpent);
    }

    [Fact]
    public async Task Create_AyniGunKayitVarsa_ConflictExceptionFirlatir()
    {
        var f = new Fixture();
        f.Notes.Setup(r => r.ExistsOnDayAsync(7, new DateTime(2026, 3, 4), null)).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => f.Service.CreateNoteAsync(7, NewRequest(new DateTime(2026, 3, 4, 9, 0, 0))));
        f.Notes.Verify(r => r.AddAsync(It.IsAny<InternshipNote>()), Times.Never);
    }

    [Fact]
    public async Task Create_GelecekTarih_InvalidOperationExceptionFirlatir()
    {
        var f = new Fixture();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.CreateNoteAsync(7, NewRequest(DateTime.UtcNow.Date.AddDays(5))));
    }

    // ---------------------------------------------------------------- düzenleme / silme kilidi

    [Theory]
    [InlineData(NoteStatus.Submitted)]
    [InlineData(NoteStatus.Approved)]
    public async Task Update_GonderilmisVeyaOnayliKayit_ConflictExceptionFirlatir(NoteStatus status)
    {
        var f = new Fixture();
        f.Existing(status);

        await Assert.ThrowsAsync<ConflictException>(() => f.Service.UpdateNoteAsync(1, 10, new UpdateNoteRequestDto { Title = "Yeni", Content = "Yeni" }));
        f.Notes.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Update_DuzeltmeIstenenKayit_DuzenlenebilirVeStatuKorunur()
    {
        var f = new Fixture();
        var note = f.Existing(NoteStatus.ReturnedForRevision);

        var result = await f.Service.UpdateNoteAsync(1, 10, new UpdateNoteRequestDto { Title = "Düzeltildi", Content = "Yeni iş", HoursSpent = 4 });

        Assert.Equal("Düzeltildi", note.Title);
        Assert.Equal(4m, note.HoursSpent);
        Assert.Equal("ReturnedForRevision", result.Status); // tekrar göndermek ayrı bir eylem
    }

    [Fact]
    public async Task Update_TarihiBaskaKayitliGuneTasirsa_ConflictExceptionFirlatir()
    {
        var f = new Fixture();
        f.Existing(NoteStatus.Draft);
        var target = DateTime.UtcNow.Date.AddDays(-3);
        f.Notes.Setup(r => r.ExistsOnDayAsync(1, target, 10)).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => f.Service.UpdateNoteAsync(1, 10, new UpdateNoteRequestDto { Title = "Başlık", Content = "İş", NoteDate = target }));
    }

    [Theory]
    [InlineData(NoteStatus.Submitted)]
    [InlineData(NoteStatus.Approved)]
    public async Task Delete_GonderilmisVeyaOnayliKayit_ConflictExceptionFirlatir(NoteStatus status)
    {
        var f = new Fixture();
        var note = f.Existing(status);

        await Assert.ThrowsAsync<ConflictException>(() => f.Service.DeleteNoteAsync(1, 10));
        Assert.False(note.IsDeleted);
    }

    [Fact]
    public async Task Delete_DuzeltmeIstenenKayit_SilinebilirSoftDelete()
    {
        var f = new Fixture();
        var note = f.Existing(NoteStatus.ReturnedForRevision);

        await f.Service.DeleteNoteAsync(1, 10);

        Assert.True(note.IsDeleted);
    }

    [Fact]
    public async Task Restore_AyniGuneYeniKayitAcilmisSa_ConflictExceptionFirlatir()
    {
        var f = new Fixture();
        var note = new InternshipNote { Id = 10, UserId = 1, IsDeleted = true, NoteDate = new DateTime(2026, 3, 4) };
        f.Notes.Setup(r => r.GetByIdIncludingDeletedAsync(10)).ReturnsAsync(note);
        f.Notes.Setup(r => r.ExistsOnDayAsync(1, new DateTime(2026, 3, 4), 10)).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => f.Service.RestoreNoteAsync(1, 10));
        Assert.True(note.IsDeleted);
    }

    // ---------------------------------------------------------------- gönder / geri çek

    [Theory]
    [InlineData(NoteStatus.Draft)]
    [InlineData(NoteStatus.ReturnedForRevision)]
    public async Task Submit_TaslakVeyaDuzeltmeIstenen_GonderildiOlurZamanDamgasiVurulur(NoteStatus from)
    {
        var f = new Fixture();
        var note = f.Existing(from);

        var result = await f.Service.SubmitNoteAsync(1, 10);

        Assert.Equal(NoteStatus.Submitted, note.Status);
        Assert.NotNull(note.SubmittedAt);
        Assert.Equal("Submitted", result.Status);
    }

    [Fact]
    public async Task Submit_BaslikVeyaIsBossa_InvalidOperationExceptionFirlatir()
    {
        var f = new Fixture();
        var note = f.Existing(NoteStatus.Draft, title: "", content: "iş"); // eski, başlıksız not

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SubmitNoteAsync(1, 10));
        Assert.Equal(NoteStatus.Draft, note.Status);
    }

    [Theory]
    [InlineData(NoteStatus.Submitted)]
    [InlineData(NoteStatus.Approved)]
    public async Task Submit_ZatenGonderilmisVeyaOnayli_ConflictExceptionFirlatir(NoteStatus status)
    {
        var f = new Fixture();
        f.Existing(status);

        await Assert.ThrowsAsync<ConflictException>(() => f.Service.SubmitNoteAsync(1, 10));
    }

    [Fact]
    public async Task Submit_BaskasininKaydi_ForbiddenExceptionFirlatir()
    {
        var f = new Fixture();
        f.Existing(NoteStatus.Draft, userId: 99);

        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SubmitNoteAsync(1, 10));
    }

    [Fact]
    public async Task Withdraw_GonderilmisKayit_TaslagaDonerVeZamanDamgasiSilinir()
    {
        var f = new Fixture();
        var note = f.Existing(NoteStatus.Submitted);
        note.SubmittedAt = DateTime.UtcNow;

        await f.Service.WithdrawNoteAsync(1, 10);

        Assert.Equal(NoteStatus.Draft, note.Status);
        Assert.Null(note.SubmittedAt);
    }

    [Theory]
    [InlineData(NoteStatus.Draft)]
    [InlineData(NoteStatus.Approved)]
    [InlineData(NoteStatus.ReturnedForRevision)]
    public async Task Withdraw_GonderilmisDegilse_ConflictExceptionFirlatir(NoteStatus status)
    {
        var f = new Fixture();
        f.Existing(status);

        await Assert.ThrowsAsync<ConflictException>(() => f.Service.WithdrawNoteAsync(1, 10));
    }

    // ---------------------------------------------------------------- mentor değerlendirmesi

    [Fact]
    public async Task Review_StajyerMentorunGrubundaDegilse_ForbiddenExceptionFirlatir()
    {
        var f = new Fixture();
        f.Existing(NoteStatus.Submitted, userId: 5);
        f.Members.Setup(r => r.IsUserInMentorGroupAsync(2, 5)).ReturnsAsync(false);

        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.ReviewNoteAsync(2, 10, new ReviewNoteRequestDto { Approve = true }));
    }

    [Theory]
    [InlineData(NoteStatus.Draft)]
    [InlineData(NoteStatus.Approved)]
    [InlineData(NoteStatus.ReturnedForRevision)]
    public async Task Review_GonderilmisDegilse_ConflictExceptionFirlatir(NoteStatus status)
    {
        var f = new Fixture();
        f.Existing(status, userId: 5);
        f.Members.Setup(r => r.IsUserInMentorGroupAsync(2, 5)).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ReviewNoteAsync(2, 10, new ReviewNoteRequestDto { Approve = true }));
    }

    [Fact]
    public async Task Review_Onay_OnaylandiOlurMentorVeZamanKaydedilir()
    {
        var f = new Fixture();
        var note = f.Existing(NoteStatus.Submitted, userId: 5);
        f.Members.Setup(r => r.IsUserInMentorGroupAsync(2, 5)).ReturnsAsync(true);
        f.Users.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<User>
        {
            new() { Id = 5, FullName = "Elif Saraç" },
            new() { Id = 2, FullName = "Zeynep Kaya" }
        });

        var result = await f.Service.ReviewNoteAsync(2, 10, new ReviewNoteRequestDto { Approve = true, Comment = " Güzel iş " });

        Assert.Equal(NoteStatus.Approved, note.Status);
        Assert.Equal("Güzel iş", note.MentorComment);
        Assert.Equal(2, note.ReviewedByUserId);
        Assert.NotNull(note.ReviewedAt);
        Assert.Equal("Zeynep Kaya", result.ReviewedByName);
        Assert.Equal("Elif Saraç", result.UserName);
    }

    [Fact]
    public async Task Review_AciklamasizDuzeltmeIstegi_InvalidOperationExceptionFirlatir()
    {
        var f = new Fixture();
        var note = f.Existing(NoteStatus.Submitted, userId: 5);
        f.Members.Setup(r => r.IsUserInMentorGroupAsync(2, 5)).ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.ReviewNoteAsync(2, 10, new ReviewNoteRequestDto { Approve = false, Comment = "   " }));
        Assert.Equal(NoteStatus.Submitted, note.Status);
    }

    [Fact]
    public async Task Review_AciklamaliDuzeltmeIstegi_DuzeltmeIstendiOlur()
    {
        var f = new Fixture();
        var note = f.Existing(NoteStatus.Submitted, userId: 5);
        f.Members.Setup(r => r.IsUserInMentorGroupAsync(2, 5)).ReturnsAsync(true);

        await f.Service.ReviewNoteAsync(2, 10, new ReviewNoteRequestDto { Approve = false, Comment = "Süreyi ekle" });

        Assert.Equal(NoteStatus.ReturnedForRevision, note.Status);
        Assert.Equal("Süreyi ekle", note.MentorComment);
    }

    // ---------------------------------------------------------------- mentor görünürlüğü

    [Fact]
    public async Task GetById_MentorStajyerinGonderilmisKaydiniGorebilir()
    {
        var f = new Fixture();
        f.Existing(NoteStatus.Submitted, userId: 5);
        f.Members.Setup(r => r.IsUserInMentorGroupAsync(2, 5)).ReturnsAsync(true);

        var result = await f.Service.GetNoteByIdAsync(callerId: 2, noteId: 10);

        Assert.Equal(10, result.Id);
    }

    [Fact]
    public async Task GetById_MentorStajyerinTaslaginiGOREMEZ()
    {
        var f = new Fixture();
        f.Existing(NoteStatus.Draft, userId: 5);
        f.Members.Setup(r => r.IsUserInMentorGroupAsync(2, 5)).ReturnsAsync(true);

        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetNoteByIdAsync(callerId: 2, noteId: 10));
    }

    [Fact]
    public async Task ReviewQueue_TaslakFiltresiIstenirse_InvalidOperationExceptionFirlatir()
    {
        var f = new Fixture();

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.GetReviewQueueAsync(2, 1, 20, "Draft", null));
    }

    [Fact]
    public async Task ReviewQueue_DurumVeAramaMetniKirpilarakRepositoryeAktarilir()
    {
        var f = new Fixture();
        f.Notes.Setup(r => r.GetPagedForMentorAsync(2, 1, 20, NoteStatus.Submitted, "elif")).ReturnsAsync((new List<InternshipNote>(), 0));

        await f.Service.GetReviewQueueAsync(2, 1, 20, "submitted", "  elif ");

        f.Notes.Verify(r => r.GetPagedForMentorAsync(2, 1, 20, NoteStatus.Submitted, "elif"), Times.Once);
    }

    [Fact]
    public async Task GetMyNotes_GecersizDurum_InvalidOperationExceptionFirlatir()
    {
        var f = new Fixture();

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.GetMyNotesAsync(1, 1, 20, "Bilinmiyor", null));
    }

    // ---------------------------------------------------------------- dışa aktarma

    [Fact]
    public async Task Export_BaslangicBitistenSonraysa_InvalidOperationExceptionFirlatir()
    {
        var f = new Fixture();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.ExportNotesAsync(1, new DateTime(2026, 5, 1), new DateTime(2026, 4, 1)));
    }

    [Fact]
    public async Task Export_RepositoryeUstSinirIleSorulur()
    {
        var f = new Fixture();
        f.Notes.Setup(r => r.GetRangeByUserIdAsync(1, null, null, 400)).ReturnsAsync(new List<InternshipNote>
        {
            new() { Id = 1, UserId = 1, Title = "A" }
        });

        var result = await f.Service.ExportNotesAsync(1, null, null);

        Assert.Single(result);
        f.Notes.Verify(r => r.GetRangeByUserIdAsync(1, null, null, 400), Times.Once);
    }
}
