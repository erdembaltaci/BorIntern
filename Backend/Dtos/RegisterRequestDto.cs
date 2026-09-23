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
    [MinLength(6, ErrorMessage = "Parola en az 6 karakter olmalı.")]
    public string Password { get; set; } = string.Empty;
}
