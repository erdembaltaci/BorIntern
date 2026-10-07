namespace Backend.Dtos;

public class AnnouncementDto
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    // Stajyer "hangi gruptan, kimden" bilgisini görebilsin diye ad alanları da döner.
    public string GroupName { get; set; } = string.Empty;
    public int MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
