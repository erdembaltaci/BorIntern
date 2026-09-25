using Backend.Repositories;

namespace Backend.BackgroundServices;

// Süresi dolmuş refresh token'lar hiçbir işe yaramaz ama tabloda birikir; bu servis
// uygulama açılınca ve sonra her 6 saatte bir bunları siler.
public class RefreshTokenCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshTokenCleanupService> _logger;

    public RefreshTokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<RefreshTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            do
            {
                try
                {
                    await CleanupAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Süresi dolan refresh token temizliği başarısız oldu.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Uygulama kapanıyor.
        }
    }

    // BackgroundService singleton olduğu için scoped repository'yi her seferinde yeni bir scope içinde alıyoruz.
    public async Task<int> CleanupAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        int deleted = await repository.DeleteExpiredAsync(DateTime.UtcNow);
        if (deleted > 0)
        {
            _logger.LogInformation("{Count} süresi dolmuş refresh token silindi.", deleted);
        }

        return deleted;
    }
}
