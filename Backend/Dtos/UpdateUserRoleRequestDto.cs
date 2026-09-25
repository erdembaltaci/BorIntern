using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class UpdateUserRoleRequestDto
{
    [Required(ErrorMessage = "Rol zorunludur.")]
    public string Role { get; set; } = string.Empty;
}
