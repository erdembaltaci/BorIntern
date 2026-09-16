namespace Backend.Entities;

public class InternshipNote : SoftDeletableEntity
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int UserId { get; set; }
    public DateTime? NoteDate { get; set; }= DateTime.UtcNow;
}