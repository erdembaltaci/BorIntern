namespace Backend.Exceptions;

// "Aradığın kayıt yok" durumları için (kullanıcı/görev/grup/not bulunamadı). Middleware bunu 404'e çevirir.
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
