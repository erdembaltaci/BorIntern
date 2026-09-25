using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Services;
using Backend.Repositories;
using Backend.Middleware;
using Backend.BackgroundServices;
using Backend.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// DI kayıtları: her Repository/Service, "IXxx istenirse Xxx ver" diye kaydediliyor.
// AddScoped = aynı HTTP isteği içinde her zaman aynı örnek kullanılır (istekten isteğe yeni örnek).
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<IGroupMemberRepository, GroupMemberRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IInternshipNoteRepository, InternshipNoteRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IGroupMemberService, GroupMemberService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IInternshipNoteService, InternshipNoteService>();
// Arka planda çalışır: süresi dolmuş refresh token'ları periyodik olarak siler.
builder.Services.AddHostedService<RefreshTokenCleanupService>();
// Hesap bazlı giriş kilidi sayaçları bellekte tutulur, bu yüzden tüm istekler için TEK örnek (Singleton) gerekir.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();
// CORS: Angular frontend farklı bir adresten (localhost:4200) istek atacağı için,
// tarayıcı bu izni görmeden isteği reddeder. appsettings'te olmayan bir origin denenirse
// istek yine reddedilir - bu, sadece "izin verdiğimiz" adreslerin bize erişebilmesi demek.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Token'ı 'Bearer {token}' formatında gir"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });

});


// JWT doğrulama ayarları: token'ın imzası, issuer/audience'ı ve süresi burada kontrol edilir.
// Token üretirken (AuthService.GenerateJwtToken) kullandığımız Jwt:Key/Issuer/Audience ile
// burada doğrulama yaparken kullandığımız değerler AYNI olmak zorunda.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        // İmza/süre geçerli olsa bile kullanıcı pasifleştirilmiş ya da rolü değişmiş olabilir: her istekte güncel hali okunur.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = CurrentUserTokenValidator.ValidateAsync
        };
    });

// Rate limiting: aynı IP'den 1 dakikada en fazla N istek, fazlası 429 (Too Many Requests) alır.
// "LoginPolicy" (5): parola denemeleri. "RegisterPolicy" (10): kayıt spam'i. "RefreshPolicy" (20): refresh token
// denemeleri; istemciler düzenli yenilediği için daha yüksek. Politikaların sayaçları birbirinden ayrıdır.
static RateLimitPartition<string> FixedWindowByIp(HttpContext httpContext, int permitLimit) =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("LoginPolicy", httpContext => FixedWindowByIp(httpContext, permitLimit: 5));
    options.AddPolicy("RegisterPolicy", httpContext => FixedWindowByIp(httpContext, permitLimit: 10));
    options.AddPolicy("RefreshPolicy", httpContext => FixedWindowByIp(httpContext, permitLimit: 20));
});

// Ters proxy (nginx, Azure vb.) arkasında gerçek istemci IP'sini X-Forwarded-For'dan okumak için. Rate limit IP'ye
// göre çalıştığı için bu olmadan herkes proxy'nin IP'si gibi görünür. Varsayılan KAPALI: güvenilir proxy adresleri
// (KnownProxies) tanımlanmadan açmak IP sahteciliğine yol açar. Açmak için: ForwardedHeaders__Enabled=true
bool useForwardedHeaders = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (useForwardedHeaders)
{
    app.UseForwardedHeaders();
}

app.UseHttpsRedirection();

// CORS, Authentication'dan ÖNCE gelmeli - tarayıcı önce "bu origin'e izin var mı" diye sorar.
app.UseCors("AllowFrontend");

// Zincirin en başında: sonrasındaki HER middleware'de (auth, controller'lar) oluşan hatayı yakalar.
// Artık controller'larda try/catch YOK - Service'ler hata fırlatır, burası merkezi olarak yakalayıp
// doğru HTTP koduna (404/401/403/400/500) çevirir.
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Sıra önemli: önce "sen kimsin" (Authentication), sonra "ne yapabilirsin" (Authorization).
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
