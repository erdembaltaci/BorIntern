using Backend.Dtos;
using Xunit;

namespace Backend.Tests;

public class PaginationTests
{
    [Theory]
    [InlineData(1, 20, 1, 20)]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-5, -1, 1, 20)]
    [InlineData(3, 500, 3, 100)]
    [InlineData(2, 10, 2, 10)]
    public void Normalize_GecersizDegerleriDuzeltir(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var result = Pagination.Normalize(page, pageSize);

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedPageSize, result.PageSize);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(45, 20, 3)]
    public void TotalPages_ToplamKayitVeSayfaBoyutundanHesaplanir(int totalCount, int pageSize, int expectedPages)
    {
        var dto = PagedResultDto<int>.Create(new List<int>(), 1, pageSize, totalCount);

        Assert.Equal(expectedPages, dto.TotalPages);
    }
}
