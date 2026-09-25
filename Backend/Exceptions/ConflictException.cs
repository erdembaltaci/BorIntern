namespace Backend.Exceptions;

// İstenen kayıt zaten var (aynı e-posta, aynı grup adı, zaten üye olan kullanıcı). 409'a çevrilir.
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
