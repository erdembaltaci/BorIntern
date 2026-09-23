using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Repositories;

public class InternshipNoteRepository : IInternshipNoteRepository
{
    private readonly AppDbContext _context;

    public InternshipNoteRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<InternshipNote?> GetByIdAsync(int id)
    {
        return await _context.InternshipNotes.FirstOrDefaultAsync(n => n.Id == id);
    }

    // IgnoreQueryFilters(): AppDbContext'teki "!IsDeleted" filtresini bu sorgu için devre dışı
    // bırakır - yoksa silinmiş bir notu asla bulamayız, restore etmek imkansız olurdu.
    public async Task<InternshipNote?> GetByIdIncludingDeletedAsync(int id)
    {
        return await _context.InternshipNotes.IgnoreQueryFilters().FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<List<InternshipNote>> GetByUserIdAsync(int userId)
    {
        return await _context.InternshipNotes.Where(n => n.UserId == userId).ToListAsync();
    }

    public async Task AddAsync(InternshipNote note)
    {
        await _context.InternshipNotes.AddAsync(note);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
