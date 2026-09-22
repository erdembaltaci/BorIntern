namespace Backend.Entities;

public class GroupMember : SoftDeletableEntity
{
    public int GroupId { get; set; }
    public int UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}