namespace Backend.Dtos;

public class TaskDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int AssignedUserId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
