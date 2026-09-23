using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class CreateNoteRequestDto
{
    [Required(ErrorMessage = "İçerik zorunludur.")]
    [MinLength(1, ErrorMessage = "İçerik boş olamaz.")]
    public string Content { get; set; } = string.Empty;

    public DateTime? NoteDate { get; set; }
}
