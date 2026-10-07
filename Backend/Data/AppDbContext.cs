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

    public DbSet<Announcement> Announcements { get; set; }

    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

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

        modelBuilder.Entity<Announcement>()
        .HasQueryFilter(announcement => !announcement.IsDeleted);

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

        // Duyurular: grup ve mentor silinemez (soft delete kullanıldığı için pratikte zaten tetiklenmez);
        // uzunluk sınırları DTO'daki kurallarla aynı, veritabanı da son savunma olarak zorlar.
        modelBuilder.Entity<Announcement>()
            .HasOne<Group>()
            .WithMany()
            .HasForeignKey(announcement => announcement.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Announcement>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(announcement => announcement.MentorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Announcement>()
            .Property(announcement => announcement.Title)
            .HasMaxLength(150);

        modelBuilder.Entity<Announcement>()
            .Property(announcement => announcement.Content)
            .HasMaxLength(2000);

        modelBuilder.Entity<Announcement>()
            .HasIndex(announcement => announcement.GroupId);

        // Staj defteri alanları: uzunluk sınırları DTO'daki kurallarla aynı (veritabanı son savunma),
        // süre 2 ondalıklı (ör. 7,50 saat), mentor-stajyer akışı için mentor Id'si kullanıcıya bağlı.
        modelBuilder.Entity<InternshipNote>(note =>
        {
            note.Property(n => n.Title).HasMaxLength(150);
            note.Property(n => n.Content).HasMaxLength(4000);
            note.Property(n => n.Learned).HasMaxLength(2000);
            note.Property(n => n.Tags).HasMaxLength(200);
            note.Property(n => n.MentorComment).HasMaxLength(1000);
            note.Property(n => n.HoursSpent).HasPrecision(4, 2);
            note.HasOne<User>().WithMany().HasForeignKey(n => n.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
            // Mentor inceleme kuyruğu ve stajyer listesi durum ile filtrelendiği için.
            note.HasIndex(n => new { n.UserId, n.Status });
        });

        // Parola sıfırlama anahtarları: sadece SHA-256 özeti (44 karakter base64) saklanır, aranabilsin diye benzersiz indeks.
        modelBuilder.Entity<PasswordResetToken>(token =>
        {
            token.Property(t => t.TokenHash).HasMaxLength(64);
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Aynı token string'i iki kere üretilmesin diye (pratikte imkansıza yakın ama garanti olsun).
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();
    }
}