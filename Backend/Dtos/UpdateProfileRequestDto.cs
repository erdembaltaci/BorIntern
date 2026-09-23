namespace Backend.Dtos;

// Kullanıcı sadece kendi adını güncelleyebilir; Email/Role/Status kasıtlı olarak burada yok.
public class UpdateProfileRequestDto
{
    public string FullName { get; set; } = string.Empty;
}
