namespace Backend.Entities;

public class InternshipNote : SoftDeletableEntity
{

    public string Content { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime? NoteDate { get; set; }= DateTime.UtcNow;
}