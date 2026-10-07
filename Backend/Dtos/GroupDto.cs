namespace Backend.Dtos;

public class GroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
