# Staj Takip Sistemi

Angular ve ASP.NET Core kullanılarak geliştirilecek basit bir fullstack staj takip uygulaması.

## Hızlı Kurulum (sıfırdan çalıştırma)

Gizli değerler (`appsettings`'te değil) User Secrets'ta tutulur; bu yüzden repo klonlandıktan sonra aşağıdaki adımlar gerekir. Ön koşullar: .NET SDK, Docker Desktop, `dotnet-ef` aracı (`dotnet tool install --global dotnet-ef`).

**1. SQL Server'ı Docker'da ayağa kaldır** (veri `borblog-sql-data` volume'ünde kalıcıdır; şifre SQL Server kurallarına uymalı: 8+ karakter, büyük/küçük harf, rakam, sembol):

```bash
docker run -d --name borblog-sql -p 1433:1433 -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=<SIFRE>" -v borblog-sql-data:/var/opt/mssql --restart unless-stopped mcr.microsoft.com/mssql/server:2022-latest
```

**2. Gizli değerleri ayarla** (`Backend` klasöründe; `UserSecretsId` `Backend.csproj`'da zaten tanımlı). `Jwt:Key` en az 32 karakter olmalı (HMAC-SHA256):

```bash
cd Backend
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=BorBlogDb;User Id=sa;Password=<SIFRE>;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "<en az 32 karakterlik rastgele bir metin>"
dotnet user-secrets set "Jwt:Issuer" "BorBlogApi"
dotnet user-secrets set "Jwt:Audience" "BorBlogClient"
```

**3. Veritabanı şemasını oluştur, çalıştır, test et:**

```bash
dotnet ef database update      # migration'lardan tabloları kurar
dotnet run                     # http://localhost:5291/swagger
dotnet test ../Backend.Tests   # unit testler
```

**4. İlk Admin'i elle ata.** Register her zaman Intern + Pending oluşturur. Diğer roller (Mentor/Admin) Admin tarafından `PUT /api/admin/users/{id}/role` ile atanır (body: `{"role":"Mentor"}`), ama ilk Admin'i bir kez SQL ile atamak gerekir (Role: 0=Intern, 1=Mentor, 2=Admin; Status: 0=Pending, 1=Active, 2=Inactive):

```sql
UPDATE Users SET Role = 2, Status = 1 WHERE Email = 'ornek@mail.com';
```

**Veritabanını başka bir sunucuya taşımak** kod değişikliği gerektirmez: yeni sunucuda `dotnet ef database update` çalıştır (veri de gerekiyorsa `BACKUP`/`RESTORE`), sonra `ConnectionStrings:DefaultConnection` değerini yeni adresle güncelle. Production'da aynı anahtar ortam değişkeninden okunur: `ConnectionStrings__DefaultConnection`.

## API Uç Noktaları (36)

Liste uç noktaları sayfalanır (`?page=1&pageSize=20`). Yetki (rol) kontrolü `[Authorize]` ile, sahiplik kontrolü (kayıt sahibi/ilgili mentor olma şartı) serviste yapılır.

| Uç nokta | Kim | Ne yapar |
|---|---|---|
| **Auth** | | |
| `POST /api/auth/register` | Herkes | Kayıt: Intern + Pending oluşur, token dönmez |
| `POST /api/auth/login` | Herkes (IP başına dakikada 5) | Giriş: sadece Active kullanıcıya JWT + refresh token |
| `POST /api/auth/refresh` | Herkes | Refresh token ile yeni token çifti (tek kullanımlık) |
| `POST /api/auth/logout` | Herkes | Refresh token'ı iptal eder |
| **Profil** | | |
| `GET /api/users/me` | Giriş yapmış herkes | Kendi profilini görür |
| `PUT /api/users/me` | Giriş yapmış herkes | Kendi adını günceller |
| **Admin** | | |
| `GET /api/admin/users` | Admin | Tüm kullanıcılar |
| `GET /api/admin/users/pending` | Admin | Onay bekleyenler (Pending) |
| `GET /api/admin/users/{id}` | Admin | Tekil kullanıcı |
| `POST /api/admin/approve-user/{id}` | Admin | Pending → Active |
| `POST /api/admin/deactivate-user/{id}` | Admin | Active → Inactive |
| `PUT /api/admin/users/{id}/role` | Admin | Rol atar (kendi rolünü değiştiremez) |
| `GET /api/admin/groups` | Admin | Tüm gruplar |
| `GET /api/admin/tasks` | Admin | Tüm görevler |
| **Grup** | | |
| `POST /api/groups` | Mentor | Grup oluşturur |
| `GET /api/groups/mine` | Mentor | Kendi grupları |
| `GET /api/groups/{id}` | Mentor (sahibi) | Tekil grup |
| `PUT /api/groups/{id}` | Mentor (sahibi) | Grup adını günceller |
| `DELETE /api/groups/{id}` | Mentor (sahibi) | Soft delete |
| `POST /api/groups/{id}/restore` | Mentor (sahibi) | Silinen grubu geri getirir |
| `POST /api/groups/{id}/members` | Mentor (sahibi) | Üye ekler |
| `GET /api/groups/{id}/members` | Grubun mentoru veya üyesi | Üyeleri listeler |
| `DELETE /api/groups/{id}/members/{userId}` | Mentor (sahibi) | Üyeyi çıkarır (soft) |
| **Görev** | | |
| `POST /api/tasks` | Mentor | Kendi grubundaki stajyere görev atar |
| `GET /api/tasks/mine` | Giriş yapmış herkes | Kendine atanan görevler |
| `GET /api/tasks/{id}` | Atanan stajyer veya oluşturan mentor | Tekil görev |
| `PUT /api/tasks/{id}/status` | Atanan stajyer | Durumu günceller (Todo/InProgress/Completed) |
| `DELETE /api/tasks/{id}` | Oluşturan mentor | Soft delete |
| `POST /api/tasks/{id}/restore` | Oluşturan mentor | Geri getirir |
| `GET /api/tasks/summary/{userId}` | Mentor (kendi grubundaki stajyer) | Durum sayıları özeti |
| **Not** | | |
| `POST /api/notes` | Giriş yapmış herkes | Günlük not ekler |
| `GET /api/notes/mine` | Giriş yapmış herkes | Kendi notları |
| `GET /api/notes/{id}` | Notun sahibi | Tekil not |
| `PUT /api/notes/{id}` | Notun sahibi | Günceller |
| `DELETE /api/notes/{id}` | Notun sahibi | Soft delete |
| `POST /api/notes/{id}/restore` | Notun sahibi | Geri getirir |

## 1. Projenin Amacı

Stajyerlerin görevlerini ve günlük staj notlarını takip etmek; yöneticinin stajyerleri ve görevleri yönetebilmesini sağlamak.

Bu proje ile şu fullstack temelleri öğrenilecek:

- Angular ile modern arayüz geliştirme
- ASP.NET Core Web API yazma
- REST API ve HTTP durum kodları
- JWT token ile giriş sistemi
- Rol ve yetki kontrolü
- Entity Framework Core ile veritabanı bağlantısı
- Azure üzerinde canlıya alma

## 2. Roller ve Kullanıcı Akışı

### Roller

- `Intern`: Stajyer — kendi görevlerini görür, günlük not ekler.
- `Mentor`: Kendi grubunu oluşturur, kendi stajyerlerine görev atar.
- `Admin`: Kullanıcıları onaylar/pasifleştirir, tüm görev ve grupları yönetir.

### Kullanıcı durumları

- `Pending`: Yeni kayıt olmuş, henüz onaylanmamış.
- `Active`: Onaylanmış, giriş yapabilir.
- `Inactive`: Pasifleştirilmiş (soft-delete yerine kullanılıyor).

### Hesap oluşturma akışı

1. Herkes kayıt ekranından hesap oluşturur.
2. Yeni kullanıcı varsayılan olarak `Intern` + `Pending` olur (kullanıcı kendi rolünü/durumunu seçemez, backend zorla atar).
3. Admin kullanıcıyı onaylar (`Active` yapar).
4. `Pending` kullanıcı giriş yapamaz.

Kullanıcı alanları:

```text
Id, FullName, Email, PasswordHash, Role, Status, CreatedAt
```

## 3. Uygulamanın Temel Özellikleri

### Stajyer (Intern)

- Kayıt olma ve giriş yapma
- Kendi görevlerini görüntüleme, durumunu güncelleme (`Todo`/`InProgress`/`Completed`)
- Günlük staj notu ekleme
- Kendi profilini görüntüleme

### Mentor

- Kendi gruplarını oluşturma
- Kendi grubundaki stajyerleri yönetme, görev atama
- Başka mentorun grubuna/stajyerine işlem yapamaz

### Admin

- Kullanıcıları onaylama, pasifleştirme ve rol atama (Mentor/Admin)
- Tüm görev ve grupları yönetme

## 4. Veri Modelleri

### User

- `Id`, `FullName`, `Email`, `PasswordHash`, `Role`, `Status`, `CreatedAt`
- Soft delete kullanılmıyor — pasifleştirme `Status = Inactive` ile yapılıyor.

### Group

- `Id`, `Name`, `MentorId`, `CreatedAt`
- Soft delete: `IsDeleted`, `DeletedAt`

### GroupMember

- `Id`, `GroupId`, `UserId`, `JoinedAt`
- Soft delete: `IsDeleted`, `DeletedAt`
- `GroupId` + `UserId` birleşik unique index

### TaskItem

- `Id`, `Title`, `Description`, `Status`, `DueDate`, `AssignedUserId`, `CreatedByUserId`, `CreatedAt`
- Soft delete: `IsDeleted`, `DeletedAt`

### InternshipNote

- `Id`, `Content`, `NoteDate`, `UserId`, `CreatedAt`
- Soft delete: `IsDeleted`, `DeletedAt`

İlişki: Bir mentor birçok grup oluşturabilir; bir grubun birçok üyesi (stajyeri) olabilir; bir kullanıcı birçok göreve ve staj notuna sahip olabilir.

## 5. Proje Yapısı

```text
BorBlog/
├── Backend/
│   ├── Controllers/          (HTTP katmanı, iş kuralı yok)
│   ├── Services/             (iş kuralları ve sahiplik kontrolleri)
│   ├── Repositories/         (veritabanı sorguları, AppDbContext sadece burada)
│   ├── Entities/             (BaseEntity, SoftDeletableEntity ve tablolar)
│   ├── Dtos/                 (istek/cevap modelleri, sayfalama)
│   ├── Data/                 (AppDbContext, soft delete query filter'ları)
│   ├── Middleware/           (ExceptionHandling, RequestLogging)
│   ├── Exceptions/           (NotFound, Unauthorized, Forbidden, Conflict)
│   ├── BackgroundServices/   (süresi dolan refresh token temizliği)
│   ├── Migrations/
│   └── Program.cs            (DI, JWT, CORS, rate limit, middleware sırası)
├── Backend.Tests/            (xUnit + Moq unit testleri)
├── Frontend/                 (henüz oluşturulmadı)
│   └── src/app/
│       ├── core/
│       ├── shared/
│       └── features/
├── BorBlog.slnx
└── README.md
```

## 6. Geliştirme Sırası

### Adım 1: Gereksinim ve tasarım

- Kullanıcı rollerini belirle.
- Veri modellerini çiz.
- Ekranları kağıt üzerinde tasarla.
- Git repository oluştur.

### Adım 2: Backend başlangıcı

- ASP.NET Core Web API oluştur.
- Swagger'ı çalıştır.
- Controller ve servis yapısını kur.
- Geliştirme ortamı için connection string ekle.

### Adım 3: Veritabanı

- Entity sınıflarını oluştur.
- `DbContext` yaz.
- Entity Framework Core bağlantısını kur.
- Migration oluştur ve veritabanını güncelle.

### Adım 4: Authentication

- Register endpoint'i yaz.
- Login endpoint'i yaz.
- Parolayı hash'leyerek sakla.
- JWT access token üret.
- Token olmadan korumalı endpoint'lere erişimi engelle.

Uç noktaların güncel ve tam listesi yukarıdaki **API Uç Noktaları** bölümünde.

### Adım 5: Angular başlangıcı

- Angular projesi oluştur.
- Routing yapısını kur.
- Login ve register ekranlarını oluştur.
- Auth service yaz.
- HTTP interceptor ile token ekle.
- Guard ile korumalı sayfaları yönet.

### Adım 6: Arayüz

- Dashboard ekranı
- Görev listesi ve görev formu
- Staj notları ekranı
- Admin kullanıcı yönetimi
- Form validasyonları
- Loading ve hata mesajları
- Mobil uyumlu tasarım

### Adım 7: Test

Şunları kontrol et:

- Hatalı giriş reddediliyor mu?
- Aynı email ile tekrar kayıt engelleniyor mu?
- Token olmadan API çalışıyor mu?
- Stajyer başka kullanıcının görevini değiştirebiliyor mu?
- Admin onaylamadan stajyer giriş yapabiliyor mu?
- Angular API hatalarını kullanıcıya gösteriyor mu?

### Adım 8: Azure'da canlıya alma

1. Azure Resource Group oluştur.
2. Azure SQL Database oluştur.
3. .NET API için Azure App Service oluştur.
4. Angular için Azure Static Web Apps oluştur.
5. Production connection string'i Azure ayarlarına ekle.
6. JWT secret'ı Azure Configuration içinde sakla.
7. API'de CORS ayarını gerçek frontend adresine göre yap.
8. GitHub Actions ile build ve deploy süreci oluştur.
9. Canlı sistemde register, login ve görev işlemlerini test et.
10. Azure bütçe uyarısı oluştur ve kullanılmayan kaynakları kapat.

## 7. Sunumda Projeyi Tanıtma Sırası

1. Projenin amacı ve çözdüğü problem
2. Kullanıcı rolleri ve iş akışı
3. Sistem mimarisi: Angular, API ve veritabanı
4. Veritabanı tabloları ve ilişkiler
5. Register, login ve JWT akışı
6. Admin ve stajyer yetkileri
7. Angular ekranlarının kısa gösterimi
8. Swagger üzerinden API gösterimi
9. Azure canlı ortam gösterimi
10. Öğrenilenler ve geliştirilebilecek özellikler

## 8. İlk Hafta Hedefi

Hafta sonunda şu akış çalışmalı:

```text
Stajyer kayıt olur
    -> Admin onaylar
    -> Stajyer giriş yapar
    -> JWT token alır
    -> Görev oluşturur
    -> Veri SQL veritabanına kaydedilir
    -> Angular ekranında görüntülenir
    -> Uygulama Azure'da çalışır
```

## 9. Şimdilik Yapılmayacaklar

- Mikroservis
- Redis
- Gelişmiş raporlama
- Dosya yükleme

Önce küçük ama uçtan uca çalışan sistemi tamamla; daha sonra özellik ekle.

RabbitMQ ve MQTT ileride, ana CRUD sistemi bittikten sonra, küçük ve ayrı bir demo olarak ele alınacak (MQTT ana sisteme entegre edilmeyecek).

## 10. Şu Ana Kadar Tamamlananlar

**Altyapı:** Git+GitHub, Docker'da SQL Server (kalıcı volume), User Secrets ile gizli veri yönetimi, `BorBlog.slnx` altında `Backend` + `Backend.Tests`.

**Mimari:** Tam N-katmanlı yapı — `Controller → Service → Repository → AppDbContext`. `BaseEntity`/`SoftDeletableEntity` ile ortak alanlar tekilleştirildi. Özel exception tipleri (`NotFoundException`/`UnauthorizedException`/`ForbiddenException`/`ConflictException`) + `ExceptionHandlingMiddleware` ile controller'larda `try/catch` yok, hatalar merkezi olarak doğru HTTP koduna çevriliyor. `RequestLoggingMiddleware` her isteğin giriş/çıkışını ve HTTP kodunu loglar.

**Auth & Güvenlik:** Register/Login/Refresh/Logout, JWT (1 saat) + refresh token (7 gün, tek kullanımlık/rotation; veritabanında ham hali değil SHA-256 özeti saklanır), rol bazlı yetkilendirme (`[Authorize(Roles=...)]`), sahiplik kontrolleri (mentor/stajyer kendi kaydına erişir), rate limiting (IP başına dakikada: login 5, kayıt 10, refresh 20), hesap bazlı geçici kilit (aynı e-postaya 5 hatalı denemede 15 dakika), parola politikası (en az 8 karakter; büyük harf, küçük harf ve rakam), tüm request DTO'larında DataAnnotations validasyonu, CORS (`localhost:4200` için hazır), çakışmalarda (aynı e-posta/grup adı/üyelik) 409 Conflict, süresi dolan refresh token'ların arka plan servisiyle (`RefreshTokenCleanupService`, açılışta ve 6 saatte bir) silinmesi. Kimliği doğrulanan her istekte kullanıcının durumu ve rolü veritabanından tekrar okunur (`CurrentUserTokenValidator`): pasifleştirilen kullanıcının token'ı ve rolü değişen kullanıcının eski yetkisi hemen geçersiz olur. Aynı anda gelen çift kayıt veritabanı unique index'iyle yakalanıp 409 döner. Ters proxy arkasında gerçek istemci IP'si için `ForwardedHeaders__Enabled=true` (varsayılan kapalı; açarken güvenilir proxy adresleri tanımlanmalı).

**Bilinen sınırlamalar:** Logout access token'ı anında öldürmez (en fazla 1 saat geçerli kalır; pasifleştirilen kullanıcı hariç). Hesap kilidi sayaçları bellekte tutulur, tek sunuculu çalışma için uygundur ve uygulama yeniden başlayınca sıfırlanır. Her kimlikli istek bir kullanıcı sorgusu daha yapar.

**Sayfalama:** Tüm liste uç noktaları `?page=1&pageSize=20` alır (varsayılan 20, en fazla 100; geçersiz değerler varsayılana çekilir). Cevap biçimi: `{ items, page, pageSize, totalCount, totalPages }`.

**Özellikler (tam CRUD, sahiplik kontrollü):**
- **User:** register/login/refresh/logout/profil (görüntüle+güncelle); Admin: onay, pasifleştirme, rol atama, listeleme (tümü/onay bekleyenler/tekil)
- **Group:** oluşturma/listeleme/tekil görüntüleme/isim güncelleme/silme(soft)/restore, Admin tüm grupları görebilir
- **GroupMember:** üye ekleme/listeleme (mentor ve üyeler görür)/çıkarma(soft)
- **Task:** oluşturma/durum güncelleme/listeleme/tekil görüntüleme/silme(soft)/restore/performans özeti, Admin tüm görevleri görebilir
- **InternshipNote:** ekleme/listeleme/tekil görüntüleme/güncelleme/silme(soft)/restore

**Test:** `Backend.Tests` içinde 112 unit test (xUnit + Moq), her serviste başarı + hata/sahiplik senaryoları kapsanmış.

**Henüz yapılmadı (bilerek sonraya bırakılan):** Angular frontend, kalıcı entegrasyon test projesi (`WebApplicationFactory` + ayrı test veritabanı), Docker Compose (Backend+SQL+RabbitMQ birlikte), RabbitMQ (register sonrası email bildirimi), Azure'a canlıya alma.

## 11. Sıradaki Adım (bir sonraki oturum)

1. Uçtan uca testleri kalıcı bir entegrasyon test projesine taşı (`WebApplicationFactory` + ayrı test veritabanı); şimdilik 36 uç nokta ayrı bir geçici veritabanında script ile doğrulandı, repo'da yok.
2. (Tartışmalı bir davranış) Admin tüm grupları görebiliyor ama bir grubun üyelerini listeleyemiyor (403); üye listesi kuralı "mentor veya üye". İstenirse Admin'e de izin verilir.
3. Angular frontend'e başlangıç: proje iskeleti, routing, auth service, interceptor (JWT'yi her isteğe otomatik ekleyen), guard.
4. Frontend geliştirilirken paralel olarak: RabbitMQ (register sonrası email bildirimi, küçük ilk kullanım).
5. Daha sonra: Docker Compose ile Backend+SQL+RabbitMQ'yu tek komutla ayağa kaldırma, Azure'a canlıya alma.
