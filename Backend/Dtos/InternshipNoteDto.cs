namespace Backend.Dtos;

public class InternshipNoteDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime? NoteDate { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
