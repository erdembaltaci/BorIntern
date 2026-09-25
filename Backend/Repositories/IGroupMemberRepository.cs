using Backend.Entities;

namespace Backend.Repositories;

public interface IGroupMemberRepository
{
    Task<bool> IsUserInGroupAsync(int groupId, int userId);
    Task<(List<GroupMember> Items, int TotalCount)> GetPagedByGroupIdAsync(int groupId, int page, int pageSize);

    // Belirli bir GroupMember kaydını (silmek/güncellemek için) bulmaya yarar.
    Task<GroupMember?> GetByGroupAndUserAsync(int groupId, int userId);

    // Bir kullanıcının, belirli bir mentor'a ait HERHANGİ bir grupta üye olup olmadığı.
    // Task atarken "bu stajyer gerçekten benim grubumda mı" kontrolü için kullanılacak.
    Task<bool> IsUserInMentorGroupAsync(int mentorId, int userId);
    Task AddAsync(GroupMember groupMember);
    Task SaveChangesAsync();
}
