namespace Backend.Dtos;

public class CreateNoteRequestDto
{
    public string Content { get; set; } = string.Empty;
    public DateTime? NoteDate { get; set; }
}
