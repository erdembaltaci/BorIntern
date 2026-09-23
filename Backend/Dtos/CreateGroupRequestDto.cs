using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class CreateGroupRequestDto
{
    [Required(ErrorMessage = "Grup adı zorunludur.")]
    [MinLength(2, ErrorMessage = "Grup adı en az 2 karakter olmalı.")]
    public string Name { get; set; } = string.Empty;
}
