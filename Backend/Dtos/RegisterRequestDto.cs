using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

// [ApiController] sayesinde bu kurallar OTOMATİK çalışır - herhangi bir if/throw yazmamıza
// gerek yok, kural ihlal edilirse controller'a hiç girilmez, 400 + hata detayı otomatik döner.
public class RegisterRequestDto
{
    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [MinLength(2, ErrorMessage = "Ad soyad en az 2 karakter olmalı.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir email adresi girin.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola zorunludur.")]
    [MinLength(8, ErrorMessage = "Parola en az 8 karakter olmalı.")]
    [RegularExpression(@"^(?=.*\p{Ll})(?=.*\p{Lu})(?=.*\d).+$",
        ErrorMessage = "Parola en az bir büyük harf, bir küçük harf ve bir rakam içermeli.")]
    public string Password { get; set; } = string.Empty;
}
