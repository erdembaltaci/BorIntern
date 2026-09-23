namespace Backend.Dtos;

public class CreateTaskRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int AssignedUserId { get; set; }
}
