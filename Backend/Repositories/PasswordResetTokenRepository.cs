using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly AppDbContext _context;

    public PasswordResetTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PasswordResetToken token)
    {
        await _context.PasswordResetTokens.AddAsync(token);
    }

    public async Task<PasswordResetToken?> GetByHashAsync(string tokenHash)
    {
        return await _context.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
    }

    // Tek SQL UPDATE: kayıtları belleğe çekmeden toplu işaretler.
    public async Task InvalidateActiveForUserAsync(int userId, DateTime utcNow)
    {
        await _context.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, utcNow));
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
