namespace Backend.Entities
{
    public class User : BaseEntity
    {
        public string FullName { get; set; }=string.Empty;
        public string Email { get; set; }=string.Empty;
        public string PasswordHash { get; set; }=string.Empty;
        public UserRole Role { get; set; }= UserRole.Intern;
        public UserStatus Status { get; set; } = UserStatus.Pending;
    }
}