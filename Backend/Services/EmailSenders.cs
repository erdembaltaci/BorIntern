using System.Net;
using System.Net.Mail;

namespace Backend.Services;

// Gerçek bir SMTP sunucusu tanımlı DEĞİLKEN kullanılır: e-postayı göndermek yerine içeriğini loga yazar.
// Not: e-posta yapılandırılmamışken AuthService artık "şifremi unuttum" bağlantısı üretmez (canlıda loglarda geçerli bir
// sıfırlama bağlantısı kalmasın); bu sınıf, ileride eklenecek diğer e-postalar için güvenli varsayılandır.
public class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    // E-posta gerçekte gitmediği için "yapılandırılmış" sayılmaz.
    public bool IsConfigured => false;

    public LogEmailSender(ILogger<LogEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string body)
    {
        _logger.LogWarning(
            "[E-POSTA - gerçekte GÖNDERİLMEDİ, SMTP ayarlı değil]\nKime: {To}\nKonu: {Subject}\n{Body}",
            toEmail, subject, body);
        return Task.CompletedTask;
    }
}

// appsettings'te "Smtp:Host" tanımlıysa kullanılır. Ayarlar: Smtp:Host, Smtp:Port (587), Smtp:User, Smtp:Password,
// Smtp:From, Smtp:EnableSsl (true). Parola ve kullanıcı adı User Secrets / ortam değişkeninde tutulmalı, koda yazılmamalı.
//
// Gönderim hatası ASLA çağırana fırlatılmaz, sadece loglanır: "şifremi unuttum"da e-postanın gönderilip gönderilemediği
// dışarıdan anlaşılırsa, bir adresin kayıtlı olup olmadığı sızardı.
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public bool IsConfigured => true;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        try
        {
            string host = _configuration["Smtp:Host"]!;
            int port = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 587;
            string from = _configuration["Smtp:From"] ?? _configuration["Smtp:User"] ?? "no-reply@pusula.local";
            bool enableSsl = !bool.TryParse(_configuration["Smtp:EnableSsl"], out var ssl) || ssl;

            using var client = new SmtpClient(host, port) { EnableSsl = enableSsl };
            string? user = _configuration["Smtp:User"];
            if (!string.IsNullOrEmpty(user))
            {
                client.Credentials = new NetworkCredential(user, _configuration["Smtp:Password"]);
            }

            using var message = new MailMessage(from, toEmail, subject, body);
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-posta gönderilemedi (alıcı: {To}).", toEmail);
        }
    }
}
