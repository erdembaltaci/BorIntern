using Backend.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Backend.Repositories;

public static class DbContextExtensions
{
    // SQL Server: 2601 (unique index) ve 2627 (unique constraint) ihlali. MySQL'e geçilirse burası 1062 olur;
    // veritabanına özgü bu ayrıntı bilerek sadece veri erişim katmanında duruyor.
    private static readonly int[] DuplicateKeyErrorNumbers = { 2601, 2627 };

    // Servis "var mı" diye kontrol edip sonra ekler; iki istek aynı anda gelirse ikisi de kontrolden geçebilir.
    // Veritabanındaki unique index son savunmadır ve bu durumda 500 yerine 409 dönmesini sağlıyoruz.
    public static async Task SaveChangesTranslatingConflictsAsync(this DbContext context)
    {
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlException
                                           && DuplicateKeyErrorNumbers.Contains(sqlException.Number))
        {
            throw new ConflictException("Bu kayıt zaten mevcut.");
        }
    }
}
