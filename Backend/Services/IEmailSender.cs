namespace Backend.Services;

// E-posta gönderimi soyutlaması: servisler "bir e-posta gönder" der, nasıl gittiğini bilmez.
// Geliştirmede gerçek SMTP olmadan çalışabilmek için iki gerçekleştirimi var (bkz. EmailSenders.cs).
public interface IEmailSender
{
    // Gerçek e-posta gönderimi tanımlı mı? false ise e-postaya dayalı özellikler ("şifremi unuttum") kapalıdır:
    // arayüz kullanıcıyı yöneticiye yönlendirir, yönetici sıfırlama bağlantısını elle iletir.
    bool IsConfigured { get; }

    Task SendAsync(string toEmail, string subject, string body);
}
