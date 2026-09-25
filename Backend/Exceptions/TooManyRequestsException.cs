namespace Backend.Exceptions;

// Çok fazla hatalı deneme (hesap geçici kilitli). 429'a çevrilir.
public class TooManyRequestsException : Exception
{
    public TooManyRequestsException(string message) : base(message) { }
}
