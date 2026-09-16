using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Entity sets for your entities    
    public DbSet<User> Users { get; set; }

    public DbSet<TaskItem> Tasks { get; set; }

    public DbSet<InternshipNote> InternshipNotes { get; set; }

    public DbSet<Group> Groups { get; set; }

    public DbSet<GroupMember> GroupMembers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // soft delete query filters
        modelBuilder.Entity<TaskItem>()
        .HasQueryFilter(task => !task.IsDeleted);

        modelBuilder.Entity<InternshipNote>()
        .HasQueryFilter(note => !note.IsDeleted);

        modelBuilder.Entity<Group>()
        .HasQueryFilter(group => !group.IsDeleted);

        modelBuilder.Entity<GroupMember>()
        .HasQueryFilter(member => !member.IsDeleted);

        // Configure relationships and constraints

        modelBuilder.Entity<User>()

            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<Group>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(group => group.MentorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GroupMember>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GroupMember>()
            .HasOne<Group>()
            .WithMany()
            .HasForeignKey(member => member.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupMember>()
            .HasIndex(member => new { member.GroupId, member.UserId })
            .IsUnique();

        modelBuilder.Entity<TaskItem>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(task => task.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TaskItem>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(task => task.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InternshipNote>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(note => note.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}