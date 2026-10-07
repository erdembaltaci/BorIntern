using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class ResetPasswordRequestDto
{
    // E-postadaki bağlantıdan gelen ham anahtar.
    [Required(ErrorMessage = "Sıfırlama bağlantısı geçersiz.")]
    [MaxLength(200, ErrorMessage = "Sıfırlama bağlantısı geçersiz.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola zorunludur.")]
    [MinLength(PasswordRules.MinLength, ErrorMessage = PasswordRules.MinLengthMessage)]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.PatternMessage)]
    public string NewPassword { get; set; } = string.Empty;
}
