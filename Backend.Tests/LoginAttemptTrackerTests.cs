using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class LoginAttemptTrackerTests
{
    // Testte zamanı elle ilerletebilmek için sahte saat.
    private sealed class TestTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static void Fail(LoginAttemptTracker tracker, string email, int times)
    {
        for (int i = 0; i < times; i++)
        {
            tracker.RecordFailure(email);
        }
    }

    [Fact]
    public void BesinciHatadaKilitlenir_DorduncudaKilitlenmez()
    {
        var tracker = new LoginAttemptTracker(new TestTimeProvider());

        Fail(tracker, "a@b.com", LoginAttemptTracker.MaxFailures - 1);
        Assert.False(tracker.IsLockedOut("a@b.com"));

        tracker.RecordFailure("a@b.com");
        Assert.True(tracker.IsLockedOut("a@b.com"));
    }

    [Fact]
    public void KilitSuresiDolunca_KilitAcilir()
    {
        var time = new TestTimeProvider();
        var tracker = new LoginAttemptTracker(time);
        Fail(tracker, "a@b.com", LoginAttemptTracker.MaxFailures);

        time.Now += LoginAttemptTracker.LockoutDuration - TimeSpan.FromSeconds(1);
        Assert.True(tracker.IsLockedOut("a@b.com"));

        time.Now += TimeSpan.FromSeconds(2);
        Assert.False(tracker.IsLockedOut("a@b.com"));
    }

    [Fact]
    public void BasariliGirisSayaciSifirlar()
    {
        var tracker = new LoginAttemptTracker(new TestTimeProvider());
        Fail(tracker, "a@b.com", LoginAttemptTracker.MaxFailures - 1);

        tracker.Reset("a@b.com");
        Fail(tracker, "a@b.com", LoginAttemptTracker.MaxFailures - 1);

        Assert.False(tracker.IsLockedOut("a@b.com"));
    }

    [Fact]
    public void FarkliEpostalarBirbirindenBagimsizdir_BuyukKucukHarfFarketmez()
    {
        var tracker = new LoginAttemptTracker(new TestTimeProvider());

        Fail(tracker, "A@B.com", LoginAttemptTracker.MaxFailures);

        Assert.True(tracker.IsLockedOut("a@b.com"));
        Assert.False(tracker.IsLockedOut("baska@b.com"));
    }

    [Fact]
    public void EskiHatalarUnutulur_UzunSureSonraTekHataKilitlemez()
    {
        var time = new TestTimeProvider();
        var tracker = new LoginAttemptTracker(time);
        Fail(tracker, "a@b.com", LoginAttemptTracker.MaxFailures - 1);

        time.Now += LoginAttemptTracker.LockoutDuration + TimeSpan.FromMinutes(1);
        tracker.RecordFailure("a@b.com");

        Assert.False(tracker.IsLockedOut("a@b.com"));
    }
}
