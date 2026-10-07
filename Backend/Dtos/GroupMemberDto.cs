namespace Backend.Dtos;

public class GroupMemberDto
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int UserId { get; set; }
    // Mentor uyeleri Id yerine adiyla gorebilsin diye (User tablosundan doldurulur).
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}
