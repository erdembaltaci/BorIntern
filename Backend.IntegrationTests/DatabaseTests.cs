using Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.IntegrationTests;

// Şema güvenliği: entity'ler ile migration'lar uyumlu mu? (Unutulan migration, canlıda "Invalid column" hatasına yol açar.)
[Collection("api")]
public class DatabaseTests
{
    private readonly TestWorld _world;

    public DatabaseTests(ApiFactory factory)
    {
        _world = new TestWorld(factory);
    }

    [Fact]
    public async Task Migrationlar_ModelleUyumlu_UygulanmamisMigrationYok()
    {
        // Model değişip migration eklenmediyse bu test kırılır ("dotnet ef migrations add ..." unutulmuştur).
        var (hasPendingModelChanges, pendingMigrations) = await _world.WithDbAsync(async db =>
            (db.Database.HasPendingModelChanges(), (await db.Database.GetPendingMigrationsAsync()).ToList()));

        Assert.False(hasPendingModelChanges, "Entity/DbContext değişti ama migration eklenmedi.");
        Assert.Empty(pendingMigrations);
    }

    [Fact]
    public async Task TumTablolarMigrationlarlaKurulmus_HepsiSorgulanabilir()
    {
        // Her tabloya basit bir sorgu: eksik tablo/sütun varsa SqlException ile kırılır.
        await _world.WithDbAsync(async db =>
        {
            await db.Users.CountAsync();
            await db.Groups.CountAsync();
            await db.GroupMembers.CountAsync();
            await db.Tasks.CountAsync();
            await db.InternshipNotes.CountAsync();
            await db.Announcements.CountAsync();
            await db.RefreshTokens.CountAsync();
            await db.PasswordResetTokens.CountAsync();
        });
    }
}
