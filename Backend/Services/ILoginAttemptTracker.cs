namespace Backend.Services;

public interface ILoginAttemptTracker
{
    bool IsLockedOut(string email);
    void RecordFailure(string email);
    void Reset(string email);
}
