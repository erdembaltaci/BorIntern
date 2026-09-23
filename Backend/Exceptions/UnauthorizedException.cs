namespace Backend.Exceptions;

// Kimlik doğrulama başarısız (yanlış parola, kullanıcı yok, hesap aktif değil). 401'e çevrilir.
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}
