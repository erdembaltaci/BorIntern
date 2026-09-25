using System.Collections.Concurrent;

namespace Backend.Services;

// Aynı e-postaya art arda hatalı giriş denemesini sayar ve hesabı geçici kilitler. Rate limit IP bazlıdır,
// bu ise hesap bazlı: farklı IP'lerden gelen saldırıyı da yavaşlatır. Sayaçlar bellekte tutulur, tek sunuculu
// çalışma için yeterlidir; uygulama yeniden başlarsa sıfırlanır.
public class LoginAttemptTracker : ILoginAttemptTracker
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const int PurgeThreshold = 10_000;

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _timeProvider;

    public LoginAttemptTracker(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public bool IsLockedOut(string email)
    {
        if (!_entries.TryGetValue(email, out var entry) || entry.LockedUntil is not { } lockedUntil)
        {
            return false;
        }

        if (lockedUntil > _timeProvider.GetUtcNow())
        {
            return true;
        }

        _entries.TryRemove(email, out _);
        return false;
    }

    public void RecordFailure(string email)
    {
        var now = _timeProvider.GetUtcNow();

        if (_entries.Count > PurgeThreshold)
        {
            Purge(now);
        }

        _entries.AddOrUpdate(
            email,
            _ => new Entry(1, now, null),
            (_, current) =>
            {
                // Son hatadan bu yana kilit süresi kadar zaman geçtiyse sayaç sıfırdan başlar.
                int failures = now - current.LastFailure > LockoutDuration ? 1 : current.Failures + 1;
                return new Entry(failures, now, failures >= MaxFailures ? now + LockoutDuration : null);
            });
    }

    public void Reset(string email)
    {
        _entries.TryRemove(email, out _);
    }

    private void Purge(DateTimeOffset now)
    {
        foreach (var pair in _entries)
        {
            if (now - pair.Value.LastFailure > LockoutDuration)
            {
                _entries.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed record Entry(int Failures, DateTimeOffset LastFailure, DateTimeOffset? LockedUntil);
}
