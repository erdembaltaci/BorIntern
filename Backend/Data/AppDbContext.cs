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

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Soft delete query filter'ları: IsDeleted=true olan kayıtlar, normal sorgularda
        // (örn. context.Tasks.ToListAsync()) otomatik olarak gizlenir - ekstra Where yazmaya gerek yok.
        // User'da bu yok çünkü User için soft delete yerine Status=Inactive kullanıyoruz.
        modelBuilder.Entity<TaskItem>()
        .HasQueryFilter(task => !task.IsDeleted);

        modelBuilder.Entity<InternshipNote>()
        .HasQueryFilter(note => !note.IsDeleted);

        modelBuilder.Entity<Group>()
        .HasQueryFilter(group => !group.IsDeleted);

        modelBuilder.Entity<GroupMember>()
        .HasQueryFilter(member => !member.IsDeleted);

        // İlişkiler ve kısıtlar.
        // Tüm foreign key'ler Restrict (silme engellenir) - GroupMember->Group hariç, o Cascade
        // (bir grup silinirse üyelik kayıtları da silinsin mantığıyla; ama Group zaten soft-delete
        // kullandığı için bu Cascade pratikte hiç tetiklenmiyor).

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

        modelBuilder.Entity<RefreshToken>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Aynı token string'i iki kere üretilmesin diye (pratikte imkansıza yakın ama garanti olsun).
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();
    }
}