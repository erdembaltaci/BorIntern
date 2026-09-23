using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class CreateTaskRequestDto
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MinLength(2, ErrorMessage = "Başlık en az 2 karakter olmalı.")]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kullanıcı Id'si girin.")]
    public int AssignedUserId { get; set; }
}
