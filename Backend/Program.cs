using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Services;
using Backend.Repositories;
using Backend.Middleware;
using Backend.BackgroundServices;
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
    });

// Rate limiting: brute-force parola denemelerine karşı. "LoginPolicy" politikası,
// aynı IP'den 1 dakikada en fazla 5 login denemesine izin verir, fazlası 429 (Too Many Requests) alır.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("LoginPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
