using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir email adresi girin.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola zorunludur.")]
    public string Password { get; set; } = string.Empty;
}
