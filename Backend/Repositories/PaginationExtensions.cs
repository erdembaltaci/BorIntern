using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Repositories;

public static class PaginationExtensions
{
    // Sayfalamada sıralama şart: OrderBy olmadan Skip/Take sayfalar arası tutarsız sonuç verebilir.
    public static async Task<(List<T> Items, int TotalCount)> ToPagedAsync<T>(
        this IQueryable<T> query, int page, int pageSize) where T : BaseEntity
    {
        int totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
