using System.Net;
using System.Net.Http.Json;
using Backend.Dtos;
using Microsoft.AspNetCore.Hosting;

namespace Backend.IntegrationTests;

/// <summary>Hız sınırlarının KENDİSİNİ sınamak için küçük sınırlarla açılan ayrı bir uygulama (kendi veritabanıyla).</summary>
public sealed class LimitedApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:Login", "5");
        builder.UseSetting("RateLimiting:Register", "3");
        builder.UseSetting("RateLimiting:Forgot", "5");
    }
}

// Her politika yalnızca BİR testte tüketilir (sayaçlar aynı uygulama içinde paylaşılır; testler birbirini etkilemesin).
public class RateLimitTests : IClassFixture<LimitedApiFactory>
{
    private readonly LimitedApiFactory _factory;

    public RateLimitTests(LimitedApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Giris_DakikadaBesDenemeSonrasi429_DigerPolitikalarEtkilenmez()
    {
        var client = _factory.CreateClient();
        var codes = new List<HttpStatusCode>();
        for (int i = 0; i < 7; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequestDto { Email = $"yok{i}@test.local", Password = "Test1234a" });
            codes.Add(response.StatusCode);
        }

        // İlk 5 istek normal işlenir (kullanıcı yok -> 401), sonrası hız sınırına takılır.
        Assert.All(codes.Take(5), c => Assert.Equal(HttpStatusCode.Unauthorized, c));
        Assert.All(codes.Skip(5), c => Assert.Equal(HttpStatusCode.TooManyRequests, c));

        // Politikaların sayaçları ayrıdır: giriş sınırı dolsa da yenileme uç noktası hâlâ yanıt verir (401, 429 değil).
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto { RefreshToken = "uydurma" });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Kayit_SinirAsilinca429()
    {
        var client = _factory.CreateClient();
        var codes = new List<HttpStatusCode>();
        for (int i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
            {
                FullName = "Kayit Deneme",
                Email = $"limit-{Guid.NewGuid():N}@test.local",
                Password = "Test1234a"
            });
            codes.Add(response.StatusCode);
        }

        Assert.All(codes.Take(3), c => Assert.Equal(HttpStatusCode.Created, c));
        Assert.All(codes.Skip(3), c => Assert.Equal(HttpStatusCode.TooManyRequests, c));
    }

    [Fact]
    public async Task SifreUnuttum_DakikadaBesIstek_SifirlamaIleAyniSinirPaylasilir()
    {
        var client = _factory.CreateClient();
        var forgot = new List<HttpStatusCode>();
        for (int i = 0; i < 7; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequestDto { Email = $"kisi{i}@test.local" });
            forgot.Add(response.StatusCode);
        }

        Assert.All(forgot.Take(5), c => Assert.Equal(HttpStatusCode.OK, c));
        Assert.All(forgot.Skip(5), c => Assert.Equal(HttpStatusCode.TooManyRequests, c));

        // "Şifremi unuttum" ve "sıfırla" aynı bütçeyi paylaşır: bütçe bittiyse sıfırlama denemeleri de durdurulur
        // (saldırgan anahtarı kaba kuvvetle de deneyemez).
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequestDto { Token = "x", NewPassword = "Aaaaaaa1" });
        Assert.Equal(HttpStatusCode.TooManyRequests, reset.StatusCode);
    }
}
