using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class ForgotPasswordRequestDto
{
    [Required(ErrorMessage = "Email zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir email adresi girin.")]
    public string Email { get; set; } = string.Empty;
}
