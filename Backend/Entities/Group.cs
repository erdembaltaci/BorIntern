using Microsoft.EntityFrameworkCore;

namespace Backend.Entities;
public class Group : SoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;
    public int MentorId { get; set; }
}