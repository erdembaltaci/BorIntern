namespace Backend.Services;

// E-posta gönderimi soyutlaması: servisler "bir e-posta gönder" der, nasıl gittiğini bilmez.
// Geliştirmede gerçek SMTP olmadan çalışabilmek için iki gerçekleştirimi var (bkz. EmailSenders.cs).
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body);
}
