using System.Net;
using System.Net.Http.Json;
using Backend.Dtos;
using Backend.Entities;

namespace Backend.IntegrationTests;

// Sunucu tarafı arama ve sayfalama: normalizasyon, büyük/küçük harf, özel karakterler (wildcard/SQL enjeksiyonu).
[Collection("api")]
public class SearchAndPagingTests
{
    private readonly TestWorld _world;

    public SearchAndPagingTests(ApiFactory factory)
    {
        _world = new TestWorld(factory);
    }

    private static async Task<PagedResultDto<T>> GetPagedAsync<T>(TestUser user, string url) =>
        await TestWorld.ReadAsync<PagedResultDto<T>>(await user.Client.GetAsync(url));

    [Fact]
    public async Task Sayfalama_GecersizDegerlerDuzeltilir_SayfaBoyutuUstSinirliVeSayfaEnAz1()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);

        var capped = await GetPagedAsync<UserDto>(admin, "/api/admin/users?page=1&pageSize=500");
        var invalid = await GetPagedAsync<UserDto>(admin, "/api/admin/users?page=-3&pageSize=0");

        Assert.Equal(100, capped.PageSize);     // en fazla 100
        Assert.Equal(1, invalid.Page);          // sayfa en az 1
        Assert.Equal(20, invalid.PageSize);     // geçersiz boyut varsayılana (20) döner
    }

    [Fact]
    public async Task Sayfalama_SinirDisiSayfa_BosListeAmaDogruToplamDoner()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);

        var page = await GetPagedAsync<UserDto>(admin, "/api/admin/users?page=9999&pageSize=10");

        Assert.Empty(page.Items);
        Assert.True(page.TotalCount >= 1);
    }

    [Fact]
    public async Task Arama_BuyukKucukHarfDuyarsiz_BastakiSondakiBoslukKirpilir_BosAramaFiltreYok()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        string marker = "Zeytin" + Guid.NewGuid().ToString("N")[..8];
        var user = await _world.RegisterAsync($"Ayşe {marker}");

        var lower = await GetPagedAsync<UserDto>(admin, $"/api/admin/users?search={marker.ToLowerInvariant()}");
        var upper = await GetPagedAsync<UserDto>(admin, $"/api/admin/users?search={marker.ToUpperInvariant()}");
        var padded = await GetPagedAsync<UserDto>(admin, $"/api/admin/users?search=%20%20{marker}%20%20");
        var byEmail = await GetPagedAsync<UserDto>(admin, $"/api/admin/users?search={Uri.EscapeDataString(user.Email)}");
        var blank = await GetPagedAsync<UserDto>(admin, "/api/admin/users?search=%20%20");
        var all = await GetPagedAsync<UserDto>(admin, "/api/admin/users");

        Assert.Equal(user.Id, Assert.Single(lower.Items).Id);
        Assert.Equal(user.Id, Assert.Single(upper.Items).Id);
        Assert.Equal(user.Id, Assert.Single(padded.Items).Id);
        Assert.Equal(user.Id, Assert.Single(byEmail.Items).Id);
        Assert.Equal(all.TotalCount, blank.TotalCount); // boşluktan ibaret arama = filtre yok
    }

    [Fact]
    public async Task Arama_OzelKarakterler_JokerOlarakYorumlanmaz_SqlEnjeksiyonuCalismaz()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        string marker = Guid.NewGuid().ToString("N")[..8];
        await _world.RegisterAsync($"Normal Kisi {marker}");
        var all = await GetPagedAsync<UserDto>(admin, "/api/admin/users?pageSize=1");

        // % ve _ SQL LIKE joker karakterleridir; düz metin olarak aranmalı (hiçbir kullanıcıda geçmiyor -> 0 sonuç).
        foreach (var query in new[] { "%", "_", "[a-z]", "'", "\"", "'; DROP TABLE Users;--", "' OR '1'='1", "\\" })
        {
            var response = await admin.Client.GetAsync("/api/admin/users?search=" + Uri.EscapeDataString(query));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<PagedResultDto<UserDto>>();
            Assert.True(result!.TotalCount < all.TotalCount, $"\"{query}\" tüm kayıtlarla eşleşti: joker gibi yorumlanıyor olabilir.");
        }

        // Tablolar yerinde (enjeksiyon denemesi bir şey silmedi).
        var after = await GetPagedAsync<UserDto>(admin, "/api/admin/users?pageSize=1");
        Assert.Equal(all.TotalCount, after.TotalCount);
    }

    [Fact]
    public async Task Arama_ToplamSayiAramaSonucunaGoredir_SayfalamaylaBirlikteCalisir()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        string marker = "Toplu" + Guid.NewGuid().ToString("N")[..8];
        for (int i = 0; i < 7; i++) await _world.RegisterAsync($"{marker} Kisi {i}");

        var page1 = await GetPagedAsync<UserDto>(admin, $"/api/admin/users?search={marker}&page=1&pageSize=5");
        var page2 = await GetPagedAsync<UserDto>(admin, $"/api/admin/users?search={marker}&page=2&pageSize=5");

        Assert.Equal(7, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(5, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Empty(page1.Items.Select(u => u.Id).Intersect(page2.Items.Select(u => u.Id)));
    }

    [Fact]
    public async Task Arama_BekleyenKullanicilarSekmesindeSadecePendingGelir()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        string marker = "Bekleme" + Guid.NewGuid().ToString("N")[..8];
        var pending = await _world.RegisterAsync($"{marker} Bekleyen");
        var active = await _world.CreateActiveAsync(UserRole.Intern, $"{marker} Aktif");

        var result = await GetPagedAsync<UserDto>(admin, $"/api/admin/users/pending?search={marker}");

        Assert.Equal(pending.Id, Assert.Single(result.Items).Id);
        Assert.DoesNotContain(result.Items, u => u.Id == active.Id);
    }

    [Fact]
    public async Task Arama_MentorGruplari_YalnizcaKendiGruplariVeAdaGoreSuzulur()
    {
        var mentor = await _world.CreateActiveAsync(UserRole.Mentor);
        var other = await _world.CreateActiveAsync(UserRole.Mentor);
        string marker = Guid.NewGuid().ToString("N")[..8];
        var mine = await _world.CreateGroupAsync(mentor, $"Yaz {marker}");
        await _world.CreateGroupAsync(mentor, $"Güz {marker}");
        await _world.CreateGroupAsync(other, $"Yaz {marker} (başkası)");

        var result = await GetPagedAsync<GroupDto>(mentor, $"/api/groups/mine?search=yaz+{marker}");

        Assert.Equal(mine.Id, Assert.Single(result.Items).Id); // diğer mentorun aynı adlı grubu sızmaz
        Assert.Equal(2, (await GetPagedAsync<GroupDto>(mentor, $"/api/groups/mine?search={marker}")).TotalCount);
    }

    [Fact]
    public async Task Arama_AdminGruplari_GrupAdinaVeMentorAdinaGoreBulur()
    {
        var admin = await _world.CreateActiveAsync(UserRole.Admin);
        string marker = Guid.NewGuid().ToString("N")[..8];
        var mentor = await _world.CreateActiveAsync(UserRole.Mentor, $"Mentor {marker}");
        var group = await _world.CreateGroupAsync(mentor, $"Ekip {Guid.NewGuid():N}"[..14]);

        var byMentorName = await GetPagedAsync<GroupDto>(admin, $"/api/admin/groups?search={marker}");
        var byGroupName = await GetPagedAsync<GroupDto>(admin, $"/api/admin/groups?search={Uri.EscapeDataString(group.Name)}");

        var found = Assert.Single(byMentorName.Items);
        Assert.Equal(group.Id, found.Id);
        Assert.Equal($"Mentor {marker}", found.MentorName);
        Assert.Contains(byGroupName.Items, g => g.Id == group.Id);
    }
}
