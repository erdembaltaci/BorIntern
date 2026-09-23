using Backend.Entities;

namespace Backend.Repositories;

public interface IGroupMemberRepository
{
    Task<bool> IsUserInGroupAsync(int groupId, int userId);
    Task<List<GroupMember>> GetByGroupIdAsync(int groupId);

    // Bir kullanıcının, belirli bir mentor'a ait HERHANGİ bir grupta üye olup olmadığı.
    // Task atarken "bu stajyer gerçekten benim grubumda mı" kontrolü için kullanılacak.
    Task<bool> IsUserInMentorGroupAsync(int mentorId, int userId);
    Task AddAsync(GroupMember groupMember);
    Task SaveChangesAsync();
}
