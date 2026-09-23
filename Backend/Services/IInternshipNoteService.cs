using Backend.Dtos;

namespace Backend.Services;

public interface IInternshipNoteService
{
    Task<InternshipNoteDto> CreateNoteAsync(int userId, CreateNoteRequestDto request);
    Task<List<InternshipNoteDto>> GetMyNotesAsync(int userId);

    // Tekil not görüntüleme - sadece notun sahibi.
    Task<InternshipNoteDto> GetNoteByIdAsync(int userId, int noteId);
    Task<InternshipNoteDto> UpdateNoteAsync(int userId, int noteId, UpdateNoteRequestDto request);
    Task DeleteNoteAsync(int userId, int noteId);

    // Soft-delete edilmiş bir notu geri getirir (IsDeleted=false yapar).
    Task<InternshipNoteDto> RestoreNoteAsync(int userId, int noteId);
}
