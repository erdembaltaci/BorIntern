using Backend.Entities;

namespace Backend.Repositories;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(int id);

    // Silinmiş (IsDeleted=true) grupları da görebilen versiyon - restore işlemi için.
    Task<Group?> GetByIdIncludingDeletedAsync(int id);

    // Duyurular gibi listelerde grup adı göstermek için birden çok grubu tek sorguda getirir.
    Task<List<Group>> GetByIdsAsync(IEnumerable<int> ids);

    // search: grup adında geçen metin (null = filtre yok).
    Task<(List<Group> Items, int TotalCount)> GetPagedByMentorIdAsync(int mentorId, int page, int pageSize, string? search);
    Task<bool> GroupNameExistsAsync(string groupName);
    Task<(List<Group> Items, int TotalCount)> GetPagedAllAsync(int page, int pageSize, string? search);
    Task AddAsync(Group group);
    Task UpdateGroupAsync(Group group);
    Task SaveChangesAsync();
}
