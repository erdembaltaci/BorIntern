namespace Backend.Dtos;

public class InternshipNoteDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    // "Yapılan iş"
    public string Content { get; set; } = string.Empty;
    public string Learned { get; set; } = string.Empty;
    public decimal? HoursSpent { get; set; }
    public string Tags { get; set; } = string.Empty;
    public DateTime? NoteDate { get; set; }

    // Draft / Submitted / Approved / ReturnedForRevision
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public string MentorComment { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
    public string ReviewedByName { get; set; } = string.Empty;

    public int UserId { get; set; }
    // Mentorun onay ekranında kaydın kime ait olduğunu göstermek için.
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
