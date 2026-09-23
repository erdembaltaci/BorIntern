using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Repositories;

// Tüm User veritabanı sorguları burada toplanır. Service katmanı, SQL/LINQ detayını bilmez.
public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _context.Users.ToListAsync();
    }

    // Admin ekranında "onay bekleyenler" gibi listeler için: duruma göre filtreli sorgu.
    public async Task<List<User>> GetByStatusAsync(UserStatus status)
    {
        return await _context.Users.Where(u => u.Status == status).ToListAsync();
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
