using Backend.Data;
using Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Backend.IntegrationTests;

/// <summary>
/// Uygulamayı gerçek HTTP hattıyla (middleware, JWT, yetkilendirme, EF Core, hız sınırı) bellek içinde başlatır.
///
/// Veritabanı: her test çalıştırması için BENZERSİZ adlı geçici bir veritabanı oluşturulur, migration'larla kurulur ve
/// bitince silinir; geliştirme veritabanına (BorBlogDb) hiç dokunulmaz. Varsayılan sunucu LocalDB'dir; başka bir SQL
/// Server için ortam değişkeni verilebilir:
///   PUSULA_TEST_CONNECTION="Server=localhost,1433;Database={db};User Id=sa;Password=...;TrustServerCertificate=True"
/// ({db} yerine benzersiz veritabanı adı konur).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"PusulaIT_{Guid.NewGuid():N}";

    public string ConnectionString { get; }

    /// <summary>Gönderilen e-postalar (SMTP yerine burada yakalanır): parola sıfırlama bağlantısını okumak için.</summary>
    public CapturingEmailSender Emails { get; } = new();

    public ApiFactory()
    {
        string template = Environment.GetEnvironmentVariable("PUSULA_TEST_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database={db};Trusted_Connection=True;TrustServerCertificate=True";
        ConnectionString = template.Replace("{db}", _databaseName);
    }

    // Host oluşmadan ÖNCE veritabanı hazır olsun (arka plan servisi açılışta tabloya bakar).
    public async Task InitializeAsync()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);
        // Migrate (EnsureCreated DEĞİL): migration'ların kendisi de her çalıştırmada sınanır.
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" ortamı: User Secrets (geliştiricinin kendi anahtarları) ve Swagger yüklenmez, test kendi kendine yeter.
        builder.UseEnvironment("Testing");

        // UseSetting: Program.cs'in en başta okuduğu değerlere (JWT anahtarı, bağlantı dizesi) de yetişir.
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("Jwt:Key", "IntegrationTestJwtKey-AtLeast32Characters-Long!!");
        builder.UseSetting("Jwt:Issuer", "PusulaTests");
        builder.UseSetting("Jwt:Audience", "PusulaTestClient");
        builder.UseSetting("App:FrontendUrl", "https://pusula.test");
        // Yüzlerce test aynı "IP"den (127.0.0.1) istek atar: hız sınırı yükseltilir. Sınırın kendisi ayrıca sınanır
        // (bkz. RateLimitTests: özel bir fabrika ile).
        foreach (string policy in new[] { "Login", "Register", "Refresh", "Forgot" })
        {
            builder.UseSetting($"RateLimiting:{policy}", "100000");
        }

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        // Bağlantı havuzundaki açık bağlantılar silmeyi engellemesin.
        SqlConnection.ClearAllPools();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
    }
}
