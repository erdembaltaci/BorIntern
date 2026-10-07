using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Backend.Services;

namespace Backend.IntegrationTests;

public sealed record SentEmail(string To, string Subject, string Body);

/// <summary>Gerçek e-posta göndermek yerine mesajları bellekte toplayan sahte gönderici.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyCollection<SentEmail> All => _sent.ToArray();

    public Task SendAsync(string toEmail, string subject, string body)
    {
        _sent.Enqueue(new SentEmail(toEmail, subject, body));
        return Task.CompletedTask;
    }

    public IReadOnlyList<SentEmail> To(string email) =>
        _sent.Where(e => string.Equals(e.To, email, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Bir kişiye gelen EN SON e-postadaki "sifre-sifirla?token=..." bağlantısından ham anahtarı çıkarır.</summary>
    public string? LastResetToken(string email)
    {
        var last = To(email).LastOrDefault();
        var match = last is null ? null : Regex.Match(last.Body, @"sifre-sifirla\?token=([A-Za-z0-9_-]+)");
        return match is { Success: true } ? match.Groups[1].Value : null;
    }
}
