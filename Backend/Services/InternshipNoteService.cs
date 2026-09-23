using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;

namespace Backend.Services;

public class InternshipNoteService : IInternshipNoteService
{
    private readonly IInternshipNoteRepository _noteRepository;

    public InternshipNoteService(IInternshipNoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    // Not her zaman token'daki kullanıcıya (userId) ait olarak oluşturulur -
    // istekte "başkası adına" not eklenmesine hiç izin verilmiyor.
    public async Task<InternshipNoteDto> CreateNoteAsync(int userId, CreateNoteRequestDto request)
    {
        var note = new InternshipNote
        {
            Content = request.Content,
            NoteDate = request.NoteDate ?? DateTime.UtcNow,
            UserId = userId
        };

        await _noteRepository.AddAsync(note);
        await _noteRepository.SaveChangesAsync();

        return MapToDto(note);
    }

    public async Task<List<InternshipNoteDto>> GetMyNotesAsync(int userId)
    {
        var notes = await _noteRepository.GetByUserIdAsync(userId);
        return notes.Select(MapToDto).ToList();
    }

    public async Task<InternshipNoteDto> UpdateNoteAsync(int userId, int noteId, UpdateNoteRequestDto request)
    {
        var note = await _noteRepository.GetByIdAsync(noteId);
        if (note == null)
        {
            throw new NotFoundException("Not bulunamadı.");
        }

        // Görev sahipliği kontrolüyle aynı mantık: sadece kendi notunu güncelleyebilirsin.
        if (note.UserId != userId)
        {
            throw new ForbiddenException("Bu not size ait değil.");
        }

        note.Content = request.Content;
        note.NoteDate = request.NoteDate ?? note.NoteDate;
        await _noteRepository.SaveChangesAsync();

        return MapToDto(note);
    }

    public async Task DeleteNoteAsync(int userId, int noteId)
    {
        var note = await _noteRepository.GetByIdAsync(noteId);
        if (note == null)
        {
            throw new NotFoundException("Not bulunamadı.");
        }

        if (note.UserId != userId)
        {
            throw new ForbiddenException("Bu not size ait değil.");
        }

        // Fiziksel silme yok - proje kararımız gereği soft delete (IsDeleted) kullanıyoruz.
        // AppDbContext'teki query filter sayesinde bu not artık listelerde görünmeyecek.
        note.IsDeleted = true;
        note.DeletedAt = DateTime.UtcNow;
        await _noteRepository.SaveChangesAsync();
    }

    public async Task<InternshipNoteDto> RestoreNoteAsync(int userId, int noteId)
    {
        // Silinmiş kayıtları da görebilen özel metodu kullanıyoruz - normal GetByIdAsync,
        // global query filter yüzünden silinmiş bir notu zaten hiç döndürmez.
        var note = await _noteRepository.GetByIdIncludingDeletedAsync(noteId);
        if (note == null)
        {
            throw new NotFoundException("Not bulunamadı.");
        }

        if (note.UserId != userId)
        {
            throw new ForbiddenException("Bu not size ait değil.");
        }

        if (!note.IsDeleted)
        {
            throw new InvalidOperationException("Bu not zaten silinmemiş.");
        }

        note.IsDeleted = false;
        note.DeletedAt = null;
        await _noteRepository.SaveChangesAsync();

        return MapToDto(note);
    }

    private static InternshipNoteDto MapToDto(InternshipNote note)
    {
        return new InternshipNoteDto
        {
            Id = note.Id,
            Content = note.Content,
            NoteDate = note.NoteDate,
            UserId = note.UserId,
            CreatedAt = note.CreatedAt
        };
    }
}
