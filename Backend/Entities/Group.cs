namespace Backend.Entities;
public class Group : SoftDeletableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MentorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}