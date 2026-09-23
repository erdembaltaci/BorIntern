namespace Backend.Dtos;

public class UpdateNoteRequestDto
{
    public string Content { get; set; } = string.Empty;
    public DateTime? NoteDate { get; set; }
}
