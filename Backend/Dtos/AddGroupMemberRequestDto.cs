using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class AddGroupMemberRequestDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kullanıcı Id'si girin.")]
    public int UserId { get; set; }
}
