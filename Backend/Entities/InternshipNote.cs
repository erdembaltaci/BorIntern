namespace Backend.Entities;

// Staj defteri kaydı (günlük). Tablo adı geçmişten gelen "InternshipNotes" olarak kaldı; mevcut notlar
// "Yapılan iş" (Content) alanına sahip taslak kayıtlar olarak korunur.
public class InternshipNote : SoftDeletableEntity
{
    public int UserId { get; set; }

    // Kaydın ait olduğu GÜN (günde tek kayıt kuralı için saat bilgisi önemsizdir).
    public DateTime? NoteDate { get; set; } = DateTime.UtcNow;

    public string Title { get; set; } = string.Empty;

    // "Yapılan iş": o gün neler yapıldı.
    public string Content { get; set; } = string.Empty;

    // "Öğrenilenler": isteğe bağlı.
    public string Learned { get; set; } = string.Empty;

    // O gün çalışılan saat (isteğe bağlı, 0.25 - 24).
    public decimal? HoursSpent { get; set; }

    // Virgülle ayrılmış etiketler (ör. "EF Core, JWT"). İleride AI ile beceri çıkarımı için yapılandırılmış veri.
    public string Tags { get; set; } = string.Empty;

    public NoteStatus Status { get; set; } = NoteStatus.Draft;

    public DateTime? SubmittedAt { get; set; }

    // Mentorun onay/düzeltme değerlendirmesi.
    public string MentorComment { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
}
