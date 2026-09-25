using System.ComponentModel.DataAnnotations;
using Backend.Dtos;
using Xunit;

namespace Backend.Tests;

public class RegisterRequestDtoTests
{
    private static List<ValidationResult> Validate(string password)
    {
        var dto = new RegisterRequestDto { FullName = "Test Kullanici", Email = "a@b.com", Password = password };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    [Theory]
    [InlineData("Abcdef12")]
    [InlineData("Test1234!")]
    [InlineData("Sifre-Cok-Uzun-Ve-Guclu-9")]
    public void GucluParola_KabulEdilir(string password)
    {
        Assert.Empty(Validate(password));
    }

    [Theory]
    [InlineData("Ab1")]        // çok kısa
    [InlineData("abcdefgh")]   // büyük harf ve rakam yok
    [InlineData("ABCDEFG1")]   // küçük harf yok
    [InlineData("Abcdefgh")]   // rakam yok
    [InlineData("abcdefg1")]   // büyük harf yok
    public void ZayifParola_Reddedilir(string password)
    {
        var results = Validate(password);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterRequestDto.Password)));
    }
}
