using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public class CreateNoteRequestDto
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MinLength(2, ErrorMessage = "Başlık en az 2 karakter olmalı.")]
    [MaxLength(150, ErrorMessage = "Başlık en fazla 150 karakter olabilir.")]
    public string Title { get; set; } = string.Empty;

    // "Yapılan iş"
    [Required(ErrorMessage = "Yapılan iş zorunludur.")]
    [MinLength(1, ErrorMessage = "Yapılan iş boş olamaz.")]
    [MaxLength(4000, ErrorMessage = "Yapılan iş en fazla 4000 karakter olabilir.")]
    public string Content { get; set; } = string.Empty;

    [MaxLength(2000, ErrorMessage = "Öğrenilenler en fazla 2000 karakter olabilir.")]
    public string? Learned { get; set; }

    // Sınırlar sayı olarak verilir (metin değil): metin biçimi sunucunun kültürüne bağlı çözülür ve Türkçe kültürde
    // "0.25" geçersiz sayılıp 500 hatasına yol açardı.
    [Range(0.25, 24, ErrorMessage = "Çalışılan süre 0,25 ile 24 saat arasında olmalı.")]
    public decimal? HoursSpent { get; set; }

    [MaxLength(200, ErrorMessage = "Etiketler en fazla 200 karakter olabilir.")]
    public string? Tags { get; set; }

    // Kaydın ait olduğu gün (boşsa bugün).
    public DateTime? NoteDate { get; set; }
}
