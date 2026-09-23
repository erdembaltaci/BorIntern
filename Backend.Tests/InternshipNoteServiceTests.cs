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
}
