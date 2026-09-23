using Backend.Entities;

namespace Backend.Repositories;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(int id);
    Task<List<Group>> GetByMentorIdAsync(int mentorId);
    Task<bool> GroupNameExistsAsync(string groupName);
    Task<List<Group>> GetAllGroupsAsync();
    Task AddAsync(Group group);
    Task UpdateGroupAsync(Group group);
    Task SaveChangesAsync();
}
