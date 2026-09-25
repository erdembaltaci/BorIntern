using System.Security.Cryptography;
using System.Text;

namespace Backend.Services;

// Refresh token'ın kendisi veritabanına yazılmaz, SHA-256 özeti yazılır: veritabanı sızsa bile token kullanılamaz.
// Token 64 baytlık rastgele bir değer olduğu için (parolanın aksine) hızlı bir hash yeterlidir.
public static class TokenHasher
{
    public static string Hash(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
