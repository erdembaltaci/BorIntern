using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class RefreshTokenRequestDto
{
    [Required(ErrorMessage = "Refresh token zorunludur.")]
    public string RefreshToken { get; set; } = string.Empty;
}
