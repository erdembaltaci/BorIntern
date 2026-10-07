using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Backend.Dtos;
using Xunit;

namespace Backend.Tests;

// Model doğrulama kuralları (attribute'lar) sunucunun kültüründen etkilenmemeli. Bu sınıf, ondalık ayracı virgül olan
// Türkçe kültürde de kuralların hata fırlatmadan çalıştığını doğrular (süre alanındaki Range kuralı bir kez 500'e yol açmıştı).
public class NoteRequestDtoValidationTests
{
    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    private static CreateNoteRequestDto Valid(decimal? hours = 6.5m) => new()
    {
        Title = "EF Core",
        Content = "Migration yazdım.",
        HoursSpent = hours
    };

    // Kültür sürekli değiştirildiği için her test sonunda eski haline döndürülür.
    private static T WithCulture<T>(string culture, Func<T> action)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void Sure_GecerliDeger_HerKulturdeHataFirlatmazVeGecerSayilir(string culture)
    {
        var results = WithCulture(culture, () => Validate(Valid(7.5m)));

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("tr-TR", 0.25)]
    [InlineData("tr-TR", 24)]
    [InlineData("en-US", 0.25)]
    [InlineData("en-US", 24)]
    public void Sure_SinirDegerleri_GecerliSayilir(string culture, double hours)
    {
        var results = WithCulture(culture, () => Validate(Valid((decimal)hours)));

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("tr-TR", 0.2)]
    [InlineData("tr-TR", 0)]
    [InlineData("tr-TR", 24.5)]
    [InlineData("tr-TR", -1)]
    [InlineData("en-US", 30)]
    public void Sure_SinirDisi_GecersizSayilir(string culture, double hours)
    {
        var results = WithCulture(culture, () => Validate(Valid((decimal)hours)));

        Assert.Single(results);
        Assert.Contains(nameof(CreateNoteRequestDto.HoursSpent), results[0].MemberNames);
    }

    [Fact]
    public void Sure_Bos_GecerliSayilir()
    {
        Assert.Empty(WithCulture("tr-TR", () => Validate(Valid(null))));
    }

    [Fact]
    public void Guncelleme_DtosuAyniSureKuraliniTasir()
    {
        var dto = new UpdateNoteRequestDto { Title = "EF Core", Content = "İş", HoursSpent = 25m };

        var results = WithCulture("tr-TR", () => Validate(dto));

        Assert.Single(results);
    }

    [Fact]
    public void Baslik_CokUzunsaVeyaKisaysaGecersizSayilir()
    {
        var tooLong = new CreateNoteRequestDto { Title = new string('x', 151), Content = "İş" };
        var tooShort = new CreateNoteRequestDto { Title = "x", Content = "İş" };

        Assert.NotEmpty(Validate(tooLong));
        Assert.NotEmpty(Validate(tooShort));
    }

    [Fact]
    public void Icerik_BosVeyaCokUzunsaGecersizSayilir()
    {
        var empty = new CreateNoteRequestDto { Title = "Başlık", Content = "" };
        var tooLong = new CreateNoteRequestDto { Title = "Başlık", Content = new string('x', 4001) };

        Assert.NotEmpty(Validate(empty));
        Assert.NotEmpty(Validate(tooLong));
    }
}
