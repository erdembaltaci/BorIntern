using Backend.Dtos;

namespace Backend.Services;

public interface IInternshipNoteService
{
    // Kayıt her zaman token'daki kullanıcıya aittir; günde tek kayıt kuralı vardır (aynı güne ikincisi 409).
    Task<InternshipNoteDto> CreateNoteAsync(int userId, CreateNoteRequestDto request);

    // status: "Draft" / "Submitted" / "Approved" / "ReturnedForRevision" (boş = hepsi). search: başlık/iş/öğrenilen/etiket.
    Task<PagedResultDto<InternshipNoteDto>> GetMyNotesAsync(int callerId, int page, int pageSize, string? status, string? search);

    // Tekil görüntüleme: kaydın sahibi ya da (taslak değilse) sahibin mentoru.
    Task<InternshipNoteDto> GetNoteByIdAsync(int callerId, int noteId);

    // Sadece Taslak ve "Düzeltme istendi" durumundaki kayıtlar düzenlenebilir.
    Task<InternshipNoteDto> UpdateNoteAsync(int userId, int noteId, UpdateNoteRequestDto request);

    // Sadece Taslak ve "Düzeltme istendi" durumundaki kayıtlar silinebilir (onaylı kayıt resmi kayıttır).
    Task DeleteNoteAsync(int userId, int noteId);

    // Soft-delete edilmiş bir notu geri getirir (IsDeleted=false yapar).
    Task<InternshipNoteDto> RestoreNoteAsync(int userId, int noteId);

    // Stajyer: taslağı (ya da düzeltme istenen kaydı) mentora gönderir / gönderileni geri çeker.
    Task<InternshipNoteDto> SubmitNoteAsync(int userId, int noteId);
    Task<InternshipNoteDto> WithdrawNoteAsync(int userId, int noteId);

    // Mentor: kendi gruplarındaki stajyerlerin gönderilmiş kayıtları.
    Task<PagedResultDto<InternshipNoteDto>> GetReviewQueueAsync(int mentorId, int page, int pageSize, string? status, string? search);

    // Mentor: gönderilmiş bir kaydı onaylar ya da düzeltme ister (düzeltmede açıklama zorunlu).
    Task<InternshipNoteDto> ReviewNoteAsync(int mentorId, int noteId, ReviewNoteRequestDto request);

    // Stajyer: yazdırılabilir defter çıktısı için tarih aralığındaki tüm kayıtları (eskiden yeniye, en fazla 400) getirir.
    Task<List<InternshipNoteDto>> ExportNotesAsync(int userId, DateTime? from, DateTime? to);
}
