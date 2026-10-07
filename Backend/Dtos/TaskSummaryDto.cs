namespace Backend.Dtos;

// Mentor'un, kendi grubundaki bir stajyerin görev durumunu özetle görmesi için.
public class TaskSummaryDto
{
    public int TotalTasks { get; set; }
    public int TodoCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }

    // Bitiş günü geçmiş ve tamamlanmamış görev sayısı (sadece stajyerin kendi özetinde doldurulur).
    public int OverdueCount { get; set; }
}
