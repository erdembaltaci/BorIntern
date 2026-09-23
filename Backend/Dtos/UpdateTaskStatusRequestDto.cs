using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

// Status'u string olarak alıyoruz (örn. "InProgress"), Service katmanında enum'a çeviriyoruz.
public class UpdateTaskStatusRequestDto
{
    [Required(ErrorMessage = "Durum zorunludur.")]
    public string Status { get; set; } = string.Empty;
}
