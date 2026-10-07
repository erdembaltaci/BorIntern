using Backend.Entities;

namespace Backend.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
    Task<bool> EmailExistsAsync(string email);
    // search: ad veya e-postada geçen metin (null = filtre yok).
    Task<(List<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);
    Task<(List<User> Items, int TotalCount)> GetPagedByStatusAsync(UserStatus status, int page, int pageSize, string? search);
    Task<List<User>> GetByIdsAsync(IEnumerable<int> ids);

    // Mentor'un gruba stajyer eklerken araması için: sadece Active + Intern, isteğe bağlı ad/e-posta araması.
    Task<(List<User> Items, int TotalCount)> GetPagedActiveInternsAsync(string? search, int page, int pageSize);
    // Mentorun KENDİ gruplarındaki aktif stajyerler (görevi devretmek için). Aynı stajyer birden çok grupta olsa da bir kez gelir.
    Task<(List<User> Items, int TotalCount)> GetPagedMentorInternsAsync(int mentorId, string? search, int page, int pageSize);
    Task AddAsync(User user);
    Task SaveChangesAsync();
}
