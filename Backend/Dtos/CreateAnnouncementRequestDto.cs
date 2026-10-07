using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class CreateAnnouncementRequestDto
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MinLength(2, ErrorMessage = "Başlık en az 2 karakter olmalı.")]
    [MaxLength(150, ErrorMessage = "Başlık en fazla 150 karakter olabilir.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Duyuru metni zorunludur.")]
    [MinLength(1, ErrorMessage = "Duyuru metni boş olamaz.")]
    [MaxLength(2000, ErrorMessage = "Duyuru metni en fazla 2000 karakter olabilir.")]
    public string Content { get; set; } = string.Empty;
}
