using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

// Kullanıcı sadece kendi adını güncelleyebilir; Email/Role/Status kasıtlı olarak burada yok.
public class UpdateProfileRequestDto
{
    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [MinLength(2, ErrorMessage = "Ad soyad en az 2 karakter olmalı.")]
    public string FullName { get; set; } = string.Empty;
}
