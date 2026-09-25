using Backend.Exceptions;

namespace Backend.Middleware;

// Tüm controller'lardaki tekrar eden try/catch'in yerine geçen TEK, merkezi hata yakalayıcı.
// Her istek buradan geçer; controller'lar artık hata yakalamak zorunda değil, sadece fırlatır.
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteResponseAsync(context, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (UnauthorizedException ex)
        {
            await WriteResponseAsync(context, StatusCodes.Status401Unauthorized, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteResponseAsync(context, StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (ConflictException ex)
        {
            await WriteResponseAsync(context, StatusCodes.Status409Conflict, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Diğer tüm "beklenen" iş kuralı hataları (örn. "email zaten kayıtlı") -> 400.
            await WriteResponseAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            // Beklenmedik gerçek hatalar: detayı istemciye göstermiyoruz (güvenlik), sadece logluyoruz.
            _logger.LogError(ex, "Beklenmeyen bir hata oluştu.");
            await WriteResponseAsync(context, StatusCodes.Status500InternalServerError, "Sunucu hatası oluştu.");
        }
    }

    private static async Task WriteResponseAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message });
    }
}
