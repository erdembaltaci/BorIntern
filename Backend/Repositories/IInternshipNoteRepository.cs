using Backend.Entities;

namespace Backend.Repositories;

public interface IInternshipNoteRepository
{
    Task<InternshipNote?> GetByIdAsync(int id);

    // Soft-delete edilmiş (IsDeleted=true) kayıtlar, AppDbContext'teki global query filter
    // yüzünden normal GetByIdAsync ile HİÇ bulunamaz. Restore işlemi için, filtreyi görmezden
    // gelen bu ayrı metoda ihtiyacımız var.
    Task<InternshipNote?> GetByIdIncludingDeletedAsync(int id);

    Task<List<InternshipNote>> GetByUserIdAsync(int userId);
    Task AddAsync(InternshipNote note);
    Task SaveChangesAsync();
}
