using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Data;
using Backend.Dtos;
using Backend.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.IntegrationTests;

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;

/// <summary>Test içinde bir kullanıcıyı temsil eder: kendi HttpClient'ı (token'lı) ile istek atar.</summary>
public sealed class TestUser
{
    public const string Password = "Test1234a";

    public int Id { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public required HttpClient Client { get; init; }

    public void UseTokens(AuthResponseDto auth)
    {
        AccessToken = auth.Token;
        RefreshToken = auth.RefreshToken;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
    }
}

/// <summary>Testlerin ortak yardımcıları: kullanıcı oluşturma/aktifleştirme ve kısa HTTP kısayolları.</summary>
public sealed class TestWorld
{
    private int _counter;
    private readonly ApiFactory _factory;

    public TestWorld(ApiFactory factory)
    {
        _factory = factory;
    }

    public HttpClient Anonymous() => _factory.CreateClient();

    // Her çağrıda benzersiz e-posta/ad: testler aynı veritabanını paylaşsa da birbirinden bağımsız kalır.
    public string UniqueEmail(string prefix) => $"{prefix}-{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@test.local";

    /// <summary>Kayıt olur (Pending). Onay/rol işini aşağıdaki metotlar yapar.</summary>
    public async Task<TestUser> RegisterAsync(string fullName, string? email = null)
    {
        email ??= UniqueEmail("kullanici");
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequestDto { FullName = fullName, Email = email, Password = TestUser.Password });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<UserDto>();
        return new TestUser { Id = created!.Id, Email = email, FullName = fullName, Client = client };
    }

    /// <summary>
    /// Kullanıcıyı doğrudan veritabanından aktif eder ve rolünü atar (ilk Admin'in elle atanması gibi),
    /// sonra giriş yapıp token'ı istemciye bağlar.
    /// </summary>
    public async Task<TestUser> CreateActiveAsync(UserRole role, string? fullName = null)
    {
        var user = await RegisterAsync(fullName ?? $"{role} Test");
        await SetStateAsync(user, role, UserStatus.Active);
        await LoginAsync(user);
        return user;
    }

    public async Task SetStateAsync(TestUser user, UserRole role, UserStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Users.FindAsync(user.Id);
        entity!.Role = role;
        entity.Status = status;
        await db.SaveChangesAsync();
    }

    public async Task<AuthResponseDto> LoginAsync(TestUser user, string? password = null)
    {
        var response = await Anonymous().PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Email = user.Email, Password = password ?? TestUser.Password });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        user.UseTokens(auth);
        return auth;
    }

    /// <summary>Testin veritabanına doğrudan bakması/müdahale etmesi gerektiğinde (ör. süresi dolmuş token simülasyonu).</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task WithDbAsync(Func<AppDbContext, Task> action) => WithDbAsync<int>(async db =>
    {
        await action(db);
        return 0;
    });

    /// <summary>Mentor + grup + gruba eklenmiş stajyer hazır senaryosu.</summary>
    public async Task<(TestUser Mentor, TestUser Intern, GroupDto Group)> MentorWithInternAsync()
    {
        var mentor = await CreateActiveAsync(UserRole.Mentor, "Mentor Zeynep");
        var intern = await CreateActiveAsync(UserRole.Intern, "Stajyer Elif");
        var group = await CreateGroupAsync(mentor);
        await AddMemberAsync(mentor, group.Id, intern);
        return (mentor, intern, group);
    }

    public async Task<GroupDto> CreateGroupAsync(TestUser mentor, string? name = null)
    {
        var response = await mentor.Client.PostAsJsonAsync("/api/groups", new CreateGroupRequestDto { Name = name ?? $"Grup {Guid.NewGuid():N}"[..20] });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GroupDto>())!;
    }

    public async Task AddMemberAsync(TestUser mentor, int groupId, TestUser intern)
    {
        var response = await mentor.Client.PostAsJsonAsync($"/api/groups/{groupId}/members", new AddGroupMemberRequestDto { UserId = intern.Id });
        response.EnsureSuccessStatusCode();
    }

    public async Task<TaskDto> AssignTaskAsync(TestUser mentor, TestUser intern, string title = "Görev", DateTime? due = null)
    {
        var response = await mentor.Client.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequestDto { Title = title, Description = "Açıklama", DueDate = due, AssignedUserId = intern.Id });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDto>())!;
    }

    public static CreateNoteRequestDto Entry(DateTime day, string title = "Defter kaydı") => new()
    {
        Title = title,
        Content = "Bugün API katmanını yazdım.",
        Learned = "DTO neden gerekli",
        HoursSpent = 6.5m,
        Tags = "ef core, jwt",
        NoteDate = day
    };

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode? expected = null)
    {
        if (expected is { } code)
        {
            Assert.Equal(code, response.StatusCode);
        }
        else
        {
            response.EnsureSuccessStatusCode();
        }

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
