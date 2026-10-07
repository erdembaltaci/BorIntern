using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class ChangePasswordRequestDto
{
    [Required(ErrorMessage = "Mevcut parola zorunludur.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola zorunludur.")]
    [MinLength(PasswordRules.MinLength, ErrorMessage = PasswordRules.MinLengthMessage)]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.PatternMessage)]
    public string NewPassword { get; set; } = string.Empty;
}
