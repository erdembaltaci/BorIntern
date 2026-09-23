namespace Backend.Dtos;

public class AuthResponseDto
{
    // Kısa ömürlü (1 saat) - her isteğin Authorization header'ında gönderilir.
    public string Token { get; set; } = string.Empty;

    // Uzun ömürlü (7 gün) - Token süresi dolunca, parola tekrar girmeden yeni Token almak için.
    public string RefreshToken { get; set; } = string.Empty;
    public UserDto User { get; set; } = new UserDto();
}
