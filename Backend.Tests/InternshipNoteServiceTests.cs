using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;
using Backend.Services;
using Moq;
using Xunit;

namespace Backend.Tests;

public class InternshipNoteServiceTests
{
    [Fact]
    public async Task CreateNoteAsync_GecerliIstek_UserIdParametredenAtanir()
    {
        var mockRepo = new Mock<IInternshipNoteRepository>();

        InternshipNote? capturedNote = null;
        mockRepo.Setup(r => r.AddAsync(It.IsAny<InternshipNote>()))
            .Callback<InternshipNote>(n => capturedNote = n)
            .Returns(Task.CompletedTask);

        var service = new InternshipNoteService(mockRepo.Object);

        var request = new CreateNoteRequestDto { Content = "Bugün EF Core öğrendim." };
        var result = await service.CreateNoteAsync(userId: 7, request);

        // userId, request'ten değil parametreden geliyor - "başkası adına not eklenemez" kuralının testi.
        Assert.Equal(7, result.UserId);
        Assert.NotNull(capturedNote);
        Assert.Equal(7, capturedNote!.UserId);
    }

    [Fact]
    public async Task UpdateNoteAsync_NotBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((InternshipNote?)null);

        var service = new InternshipNoteService(mockRepo.Object);

        var request = new UpdateNoteRequestDto { Content = "Güncel içerik" };

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateNoteAsync(userId: 1, noteId: 99, request));
    }

    [Fact]
    public async Task UpdateNoteAsync_BaskasininNotu_ForbiddenExceptionFirlatir()
    {
        var note = new InternshipNote { Id = 1, Content = "Eski içerik", UserId = 5 };

        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(note);

        var service = new InternshipNoteService(mockRepo.Object);

        var request = new UpdateNoteRequestDto { Content = "Güncel içerik" };

        // userId=1 gönderiyoruz ama not 5 numaralı kullanıcıya ait -> Forbidden beklenir.
        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateNoteAsync(userId: 1, noteId: 1, request));
    }

    [Fact]
    public async Task UpdateNoteAsync_GecerliIstek_IcerikGuncellenir()
    {
        var note = new InternshipNote { Id = 1, Content = "Eski içerik", UserId = 1 };

        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(note);

        var service = new InternshipNoteService(mockRepo.Object);

        var request = new UpdateNoteRequestDto { Content = "Güncel içerik" };
        var result = await service.UpdateNoteAsync(userId: 1, noteId: 1, request);

        Assert.Equal("Güncel içerik", result.Content);
    }

    [Fact]
    public async Task DeleteNoteAsync_BaskasininNotu_ForbiddenExceptionFirlatir()
    {
        var note = new InternshipNote { Id = 1, Content = "İçerik", UserId = 5 };

        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(note);

        var service = new InternshipNoteService(mockRepo.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteNoteAsync(userId: 1, noteId: 1));
    }

    [Fact]
    public async Task DeleteNoteAsync_GecerliIstek_SoftDeleteYapilirFizikselSilinmez()
    {
        var note = new InternshipNote { Id = 1, Content = "İçerik", UserId = 1, IsDeleted = false };

        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(note);

        var service = new InternshipNoteService(mockRepo.Object);

        await service.DeleteNoteAsync(userId: 1, noteId: 1);

        // Fiziksel silme (Remove) yerine, sadece IsDeleted=true yapıldığını doğruluyoruz -
        // tam da projenin baştaki "soft delete" tasarım kararının testi.
        Assert.True(note.IsDeleted);
        Assert.NotNull(note.DeletedAt);
    }

    [Fact]
    public async Task GetNoteByIdAsync_NotBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((InternshipNote?)null);

        var service = new InternshipNoteService(mockRepo.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetNoteByIdAsync(userId: 1, noteId: 99));
    }

    [Fact]
    public async Task GetNoteByIdAsync_BaskasininNotu_ForbiddenExceptionFirlatir()
    {
        var note = new InternshipNote { Id = 1, Content = "İçerik", UserId = 5 };

        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(note);

        var service = new InternshipNoteService(mockRepo.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetNoteByIdAsync(userId: 1, noteId: 1));
    }

    [Fact]
    public async Task GetNoteByIdAsync_GecerliIstek_NotuDoner()
    {
        var note = new InternshipNote { Id = 1, Content = "İçerik", UserId = 1 };

        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(note);

        var service = new InternshipNoteService(mockRepo.Object);

        var result = await service.GetNoteByIdAsync(userId: 1, noteId: 1);

        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task RestoreNoteAsync_NotBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdIncludingDeletedAsync(It.IsAny<int>())).ReturnsAsync((InternshipNote?)null);

        var service = new InternshipNoteService(mockRepo.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => service.RestoreNoteAsync(userId: 1, noteId: 99));
    }

    [Fact]
    public async Task RestoreNoteAsync_BaskasininNotu_ForbiddenExceptionFirlatir()
    {
        var note = new InternshipNote { Id = 1, Content = "İçerik", UserId = 5, IsDeleted = true, DeletedAt = DateTime.UtcNow };

        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdIncludingDeletedAsync(1)).ReturnsAsync(note);

        var service = new InternshipNoteService(mockRepo.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.RestoreNoteAsync(userId: 1, noteId: 1));
    }

    [Fact]
    public async Task RestoreNoteAsync_ZatenSilinmemisse_InvalidOperationExceptionFirlatir()
    {
        var note = new InternshipNote { Id = 1, Content = "İçerik", UserId = 1, IsDeleted = false };

        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdIncludingDeletedAsync(1)).ReturnsAsync(note);

        var service = new InternshipNoteService(mockRepo.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreNoteAsync(userId: 1, noteId: 1));
    }

    [Fact]
    public async Task RestoreNoteAsync_GecerliIstek_IsDeletedFalseOlur()
    {
        var note = new InternshipNote { Id = 1, Content = "İçerik", UserId = 1, IsDeleted = true, DeletedAt = DateTime.UtcNow };

        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetByIdIncludingDeletedAsync(1)).ReturnsAsync(note);

        var service = new InternshipNoteService(mockRepo.Object);

        var result = await service.RestoreNoteAsync(userId: 1, noteId: 1);

        Assert.False(note.IsDeleted);
        Assert.Null(note.DeletedAt);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task GetMyNotesAsync_SayfaBilgisiVeToplamKayitSayisiDoner()
    {
        var mockRepo = new Mock<IInternshipNoteRepository>();
        mockRepo.Setup(r => r.GetPagedByUserIdAsync(7, 1, 20)).ReturnsAsync((new List<InternshipNote>
        {
            new() { Id = 1, Content = "Not 1", UserId = 7 },
            new() { Id = 2, Content = "Not 2", UserId = 7 }
        }, 45));

        var service = new InternshipNoteService(mockRepo.Object);

        var result = await service.GetMyNotesAsync(userId: 7, page: 1, pageSize: 20);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(45, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }
}
