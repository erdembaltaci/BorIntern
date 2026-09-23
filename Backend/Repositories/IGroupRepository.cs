using Backend.Entities;

namespace Backend.Repositories;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(int id);

    // Silinmiş (IsDeleted=true) grupları da görebilen versiyon - restore işlemi için.
    Task<Group?> GetByIdIncludingDeletedAsync(int id);

    Task<List<Group>> GetByMentorIdAsync(int mentorId);
    Task<bool> GroupNameExistsAsync(string groupName);
    Task<List<Group>> GetAllGroupsAsync();
    Task AddAsync(Group group);
    Task UpdateGroupAsync(Group group);
    Task SaveChangesAsync();
}
