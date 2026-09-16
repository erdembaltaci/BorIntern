namespace Backend.Entities;

public class TaskItem : SoftDeletableEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.Todo;
    public DateTime? DueDate { get; set; }
    public int AssignedUserId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }= DateTime.UtcNow;
}