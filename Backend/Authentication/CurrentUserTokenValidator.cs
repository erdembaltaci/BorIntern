using System.Security.Claims;
using Backend.Entities;
using Backend.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Backend.Authentication;

// İmza ve süre doğrulaması geçen bir token için ek kontrol. Token 1 saat geçerli olsa da kullanıcı bu sürede
// pasifleştirilmiş ya da rolü değiştirilmiş olabilir; bu yüzden her istekte kullanıcı güncel haliyle okunur.
// Bedeli: kimliği doğrulanan her istekte bir veritabanı sorgusu.
public static class CurrentUserTokenValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var userIdValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdValue, out int userId))
        {
            context.Fail("Geçersiz token.");
            return;
        }

        var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null || user.Status != UserStatus.Active)
        {
            context.Fail("Kullanıcı aktif değil.");
            return;
        }

        // Token'daki rol eskimiş olabilir: güncel rolü veritabanından alıp claim'i değiştiriyoruz.
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        foreach (var roleClaim in identity.FindAll(ClaimTypes.Role).ToList())
        {
            identity.RemoveClaim(roleClaim);
        }

        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
    }
}
