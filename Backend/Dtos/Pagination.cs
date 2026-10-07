namespace Backend.Dtos;

public static class Pagination
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    // Arama kutusundan gelen metin: baştaki/sondaki boşluklar atılır, boşsa "arama yok" (null) sayılır.
    public static string? NormalizeSearch(string? search)
    {
        return string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    }

    // Geçersiz değerler hata vermek yerine makul varsayılana çekilir; tek seferde çok kayıt istenemesin diye üst sınır var.
    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        int safePage = Math.Max(1, page);
        int safePageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return (safePage, safePageSize);
    }
}
