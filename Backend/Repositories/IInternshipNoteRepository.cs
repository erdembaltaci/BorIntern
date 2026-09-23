using Backend.Entities;

namespace Backend.Repositories;

public interface IInternshipNoteRepository
{
    Task<InternshipNote?> GetByIdAsync(int id);
    Task<List<InternshipNote>> GetByUserIdAsync(int userId);
    Task AddAsync(InternshipNote note);
    Task SaveChangesAsync();
}
