namespace Backend.Exceptions;

// Kimliğin doğru ama bu işleme YETKİN yok (sahiplik kontrolü: "bu senin grubun/görevin/notun değil"). 403'e çevrilir.
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
