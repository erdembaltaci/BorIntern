using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

// Mentorun bir defter kaydı için kararı: onayla (Approve=true) ya da düzeltme iste (Approve=false, açıklama zorunlu).
public class ReviewNoteRequestDto
{
    public bool Approve { get; set; }

    [MaxLength(1000, ErrorMessage = "Yorum en fazla 1000 karakter olabilir.")]
    public string? Comment { get; set; }
}
