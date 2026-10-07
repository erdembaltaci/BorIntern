using Backend.Entities;

namespace Backend.Repositories;

public interface IInternshipNoteRepository
{
    Task<InternshipNote?> GetByIdAsync(int id);

    // Soft-delete edilmiş (IsDeleted=true) kayıtlar, AppDbContext'teki global query filter
    // yüzünden normal GetByIdAsync ile HİÇ bulunamaz. Restore işlemi için, filtreyi görmezden
    // gelen bu ayrı metoda ihtiyacımız var.
    Task<InternshipNote?> GetByIdIncludingDeletedAsync(int id);

    // Stajyerin kendi kayıtları, tarihe göre en yeni önce. status ve search isteğe bağlıdır (null = filtre yok);
    // search başlık, yapılan iş, öğrenilenler ve etiketlerde aranır.
    Task<(List<InternshipNote> Items, int TotalCount)> GetPagedByUserIdAsync(
        int userId, int page, int pageSize, NoteStatus? status, string? search);

    // Mentorun inceleme listesi: sadece KENDİ gruplarındaki stajyerlerin, taslak olmayan (gönderilmiş) kayıtları.
    // search ayrıca stajyerin adı/e-postasında da aranır.
    Task<(List<InternshipNote> Items, int TotalCount)> GetPagedForMentorAsync(
        int mentorId, int page, int pageSize, NoteStatus? status, string? search);

    // Dışa aktarma (yazdırma) için: bir stajyerin kayıtları, tarihe göre ESKİDEN YENİYE, en fazla `max` adet.
    Task<List<InternshipNote>> GetRangeByUserIdAsync(int userId, DateTime? from, DateTime? to, int max);

    // Günde tek kayıt kuralı için: bu kullanıcının o güne ait (silinmemiş) kaydı var mı? excludeNoteId güncellemede kaydın kendisini sayma.
    Task<bool> ExistsOnDayAsync(int userId, DateTime day, int? excludeNoteId);

    Task AddAsync(InternshipNote note);
    Task SaveChangesAsync();
}
