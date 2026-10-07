using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

// Mentorun, atadığı görevin bilgilerini düzenlemesi. Durum (Todo/InProgress/Completed) bu istekle DEĞİŞMEZ:
// durumu sadece görevin sahibi olan stajyer değiştirir. Tek istisna: görev başka stajyere devredilirse durum Todo'ya döner.
public class UpdateTaskRequestDto
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MinLength(2, ErrorMessage = "Başlık en az 2 karakter olmalı.")]
    [MaxLength(150, ErrorMessage = "Başlık en fazla 150 karakter olabilir.")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000, ErrorMessage = "Açıklama en fazla 2000 karakter olabilir.")]
    public string Description { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    // Mevcut atananla aynıysa devir yapılmaz; farklıysa yeni stajyer mentorun kendi grubunda olmalı.
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kullanıcı Id'si girin.")]
    public int AssignedUserId { get; set; }
}
