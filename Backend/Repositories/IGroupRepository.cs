using Backend.Entities;

namespace Backend.Repositories;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(int id);

    // Silinmiş (IsDeleted=true) grupları da görebilen versiyon - restore işlemi için.
    Task<Group?> GetByIdIncludingDeletedAsync(int id);

    Task<(List<Group> Items, int TotalCount)> GetPagedByMentorIdAsync(int mentorId, int page, int pageSize);
    Task<bool> GroupNameExistsAsync(string groupName);
    Task<(List<Group> Items, int TotalCount)> GetPagedAllAsync(int page, int pageSize);
    Task AddAsync(Group group);
    Task UpdateGroupAsync(Group group);
    Task SaveChangesAsync();
}
