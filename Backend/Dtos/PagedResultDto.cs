namespace Backend.Dtos;

public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResultDto<T> Create(List<T> items, int page, int pageSize, int totalCount)
    {
        return new PagedResultDto<T> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }
}
