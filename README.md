# Pusula

Stajyerlerin görevlerini, günlük defterlerini ve mentor geri bildirimlerini tek yerden yöneten fullstack bir staj takip uygulaması (Angular + ASP.NET Core + SQL Server). Adı: Pusula — mentor yön gösterir, stajyer yolunu bulur.

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
dotnet test ../Backend.Tests              # unit testler (hızlı, veritabanı gerekmez)
dotnet test ../Backend.IntegrationTests   # entegrasyon testleri (LocalDB gerekir, ayrı geçici veritabanı kurar/siler)
```

**4. İlk Admin'i elle ata.** Register her zaman Intern + Pending oluşturur. Diğer roller (Mentor/Admin) Admin tarafından `PUT /api/admin/users/{id}/role` ile atanır (body: `{"role":"Mentor"}`), ama ilk Admin'i bir kez SQL ile atamak gerekir (Role: 0=Intern, 1=Mentor, 2=Admin; Status: 0=Pending, 1=Active, 2=Inactive):

```sql
UPDATE Users SET Role = 2, Status = 1 WHERE Email = 'ornek@mail.com';
```

**5. E-posta (parola sıfırlama).** `Smtp:Host` tanımlı DEĞİLSE e-posta gerçekten gönderilmez; içeriği (sıfırlama bağlantısı dahil) backend konsoluna/loguna yazılır. Gerçek gönderim için User Secrets ya da ortam değişkeni kullan (parola koda/appsettings'e yazılmaz):

```bash
dotnet user-secrets set "Smtp:Host" "smtp.ornek.com"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:User" "kullanici@ornek.com"
dotnet user-secrets set "Smtp:Password" "<SIFRE>"
dotnet user-secrets set "Smtp:From" "Pusula <no-reply@ornek.com>"
dotnet user-secrets set "App:FrontendUrl" "https://pusula.ornek.com"   # e-postadaki bağlantının adresi (varsayılan http://localhost:4200)
```

SMTP gönderimi (`SmtpEmailSender`) yazıldı ama gerçek bir SMTP sunucusuna karşı denenmedi. Hata verirse çağırana fırlatılmaz, sadece loglanır (adres kayıtlı mı sızmasın diye).

**Hız sınırları** (IP başına dakikada) yapılandırılabilir: `RateLimiting:Login` (5), `RateLimiting:Register` (10), `RateLimiting:Refresh` (20), `RateLimiting:Forgot` (5). Otomatik testlerde yükseltmek için ortam değişkeni: `RateLimiting__Login=500`.

**Veritabanını başka bir sunucuya taşımak** kod değişikliği gerektirmez: yeni sunucuda `dotnet ef database update` çalıştır (veri de gerekiyorsa `BACKUP`/`RESTORE`), sonra `ConnectionStrings:DefaultConnection` değerini yeni adresle güncelle. Production'da aynı anahtar ortam değişkeninden okunur: `ConnectionStrings__DefaultConnection`.

## API Uç Noktaları (54)

Liste uç noktaları sayfalanır (`?page=1&pageSize=20`). Mentor/Admin listeleri (`/groups/mine`, `/tasks/created`, `/admin/users`, `/admin/users/pending`, `/admin/groups`, `/admin/tasks`) ayrıca `?search=` ile sunucu tarafında aranır: kullanıcıda ad/e-posta, grupta grup adı (admin için mentor adı da), görevde başlık/açıklama/stajyer ve mentor adı. Yetki (rol) kontrolü `[Authorize]` ile, sahiplik kontrolü (kayıt sahibi/ilgili mentor olma şartı) serviste yapılır.

| Uç nokta | Kim | Ne yapar |
|---|---|---|
| **Auth** | | |
| `POST /api/auth/register` | Herkes | Kayıt: Intern + Pending oluşur, token dönmez |
| `POST /api/auth/login` | Herkes (IP başına dakikada 5) | Giriş: sadece Active kullanıcıya JWT + refresh token |
| `POST /api/auth/refresh` | Herkes | Refresh token ile yeni token çifti (tek kullanımlık) |
| `POST /api/auth/logout` | Herkes | Refresh token'ı iptal eder |
| `POST /api/auth/change-password` | Giriş yapmış herkes (dakikada 5) | Parola değiştirir (mevcut parola doğrulanır); diğer cihazlardaki TÜM oturumlar kapanır, bu oturum yeni token çifti alır |
| `POST /api/auth/forgot-password` | Herkes (dakikada 5) | Parola sıfırlama bağlantısı yollar; adres kayıtlı olsun olmasın aynı cevap (kullanıcı sızmaz) |
| `POST /api/auth/reset-password` | Herkes (dakikada 5) | E-postadaki anahtarla yeni parola belirler (tek kullanımlık, 30 dk, tüm oturumlar kapanır) |
| **Profil** | | |
| `GET /api/users/me` | Giriş yapmış herkes | Kendi profilini görür |
| `PUT /api/users/me` | Giriş yapmış herkes | Kendi adını günceller |
| `GET /api/users/interns?search=` | Mentor | Aktif stajyerleri ad/e-posta ile arar (gruba eklemek için) |
| `GET /api/users/my-interns?search=` | Mentor | SADECE kendi gruplarındaki aktif stajyerler (görev devri seçicisi) |
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
| `POST /api/groups/{id}/members` | Mentor (sahibi) | Üye ekler (sadece aktif stajyer; yoksa 404, stajyer değilse 400) |
| `GET /api/groups/{id}/members` | Grubun mentoru veya üyesi | Üyeleri listeler (ad ve e-posta dahil) |
| `DELETE /api/groups/{id}/members/{userId}` | Mentor (sahibi) | Üyeyi çıkarır (soft) |
| **Görev** | | |
| `POST /api/tasks` | Mentor | Kendi grubundaki stajyere görev atar |
| `GET /api/tasks/mine?status=` | Giriş yapmış herkes | Kendine atanan görevler (durum sunucuda süzülür, sayfalı) |
| `GET /api/tasks/mine/summary` | Giriş yapmış herkes | Durum sayıları ve geciken görev sayısı (tek gruplu sorgu) |
| `GET /api/tasks/mine/upcoming?take=` | Giriş yapmış herkes | Bitiş tarihi en yakın bitmemiş görevler |
| `GET /api/tasks/created` | Mentor | Kendi atadığı görevler |
| `GET /api/tasks/{id}` | Atanan stajyer veya oluşturan mentor | Tekil görev |
| `PUT /api/tasks/{id}/status` | Atanan stajyer | Durumu günceller (Todo/InProgress/Completed) |
| `PUT /api/tasks/{id}` | Oluşturan mentor | Başlık/açıklama/tarih düzenler; başka stajyere devreder (yeni stajyer mentorun grubunda olmalı, durum Todo'ya döner) |
| `DELETE /api/tasks/{id}` | Oluşturan mentor | Soft delete |
| `POST /api/tasks/{id}/restore` | Oluşturan mentor | Geri getirir |
| `GET /api/tasks/summary/{userId}` | Mentor (kendi grubundaki stajyer) | Durum sayıları özeti |
| **Duyuru** | | |
| `POST /api/groups/{id}/announcements` | Mentor (sahibi) | Gruba duyuru gönderir (başlık ≤150, metin ≤2000 karakter) |
| `GET /api/groups/{id}/announcements` | Grubun mentoru veya üyesi | Grubun duyuruları, en yeni önce |
| `GET /api/announcements/mine` | Giriş yapmış herkes | Üyesi olduğum tüm grupların duyuruları (grup ve mentor adıyla) |
| `DELETE /api/groups/{id}/announcements/{announcementId}` | Mentor (sahibi) | Soft delete |
| **Staj defteri** | | |
| `POST /api/notes` | Giriş yapmış herkes | Defter kaydı ekler (başlık, yapılan iş, öğrenilen, süre, etiket); günde tek kayıt (aynı güne ikinci kayıt 409) |
| `GET /api/notes/mine?status=&search=` | Giriş yapmış herkes | Kendi kayıtları, tarihe göre en yeni önce (sayfalı) |
| `GET /api/notes/{id}` | Sahibi veya (taslak değilse) sahibinin mentoru | Tekil kayıt |
| `PUT /api/notes/{id}` | Sahibi | Günceller (sadece Taslak / Düzeltme istendi durumunda) |
| `DELETE /api/notes/{id}` | Sahibi | Soft delete (sadece Taslak / Düzeltme istendi durumunda) |
| `POST /api/notes/{id}/restore` | Sahibi | Geri getirir |
| `POST /api/notes/{id}/submit` | Sahibi | Kaydı mentora gönderir (Onay bekliyor) |
| `POST /api/notes/{id}/withdraw` | Sahibi | Henüz değerlendirilmemiş kaydı geri çeker (Taslak) |
| `GET /api/notes/review?status=&search=` | Mentor | Kendi gruplarındaki stajyerlerin gönderilmiş kayıtları (taslaklar görünmez) |
| `POST /api/notes/{id}/review` | Mentor (stajyerin mentoru) | Onaylar ya da açıklamayla düzeltme ister |
| `GET /api/notes/export?from=&to=` | Giriş yapmış herkes | Yazdırılabilir defter için tarih aralığındaki kendi kayıtları (eskiden yeniye, en fazla 400) |

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

### Announcement

- `Id`, `GroupId`, `MentorId`, `Title`, `Content`, `CreatedAt`
- Soft delete: `IsDeleted`, `DeletedAt`

### PasswordResetToken

- `Id`, `UserId`, `TokenHash` (ham anahtar saklanmaz, SHA-256 özeti), `ExpiresAt` (30 dk), `UsedAt`, `CreatedAt`

### InternshipNote (staj defteri kaydı)

- `Id`, `UserId`, `NoteDate` (gün), `Title`, `Content` ("yapılan iş"), `Learned`, `HoursSpent`, `Tags`, `CreatedAt`
- Onay akışı: `Status` (Draft → Submitted → Approved / ReturnedForRevision), `SubmittedAt`, `MentorComment`, `ReviewedAt`, `ReviewedByUserId`
- Kurallar: günde tek kayıt; sadece Draft ve ReturnedForRevision düzenlenir/silinir; Submitted geri çekilebilir; Approved kilitlidir. Taslaklar mentora görünmez.
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
├── Backend.IntegrationTests/ (WebApplicationFactory: gerçek HTTP hattı + geçici SQL Server veritabanı)
├── Frontend/                 (Angular 22, standalone + signals, lazy-load sayfalar)
│   └── src/app/
│       ├── core/             (ApiService, AuthService, interceptor, guard'lar, modeller)
│       ├── shared/           (ikon, rozet, modal, toast, sayfalama gibi ortak bileşenler)
│       ├── layout/           (yan menülü ana düzen)
│       └── features/         (auth, dashboard, tasks, notes, groups, admin, profile)
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

**Altyapı:** Git+GitHub, Docker'da SQL Server (kalıcı volume), User Secrets ile gizli veri yönetimi, `BorBlog.slnx` altında `Backend` + `Backend.Tests` + `Backend.IntegrationTests`.

**Mimari:** Tam N-katmanlı yapı — `Controller → Service → Repository → AppDbContext`. `BaseEntity`/`SoftDeletableEntity` ile ortak alanlar tekilleştirildi. Özel exception tipleri (`NotFoundException`/`UnauthorizedException`/`ForbiddenException`/`ConflictException`) + `ExceptionHandlingMiddleware` ile controller'larda `try/catch` yok, hatalar merkezi olarak doğru HTTP koduna çevriliyor. `RequestLoggingMiddleware` her isteğin giriş/çıkışını ve HTTP kodunu loglar.

**Auth & Güvenlik:** Register/Login/Refresh/Logout, JWT (1 saat) + refresh token (7 gün, tek kullanımlık/rotation; veritabanında ham hali değil SHA-256 özeti saklanır), rol bazlı yetkilendirme (`[Authorize(Roles=...)]`), sahiplik kontrolleri (mentor/stajyer kendi kaydına erişir), rate limiting (IP başına dakikada: login 5, kayıt 10, refresh 20), hesap bazlı geçici kilit (aynı e-postaya 5 hatalı denemede 15 dakika), parola politikası (en az 8 karakter; büyük harf, küçük harf ve rakam), tüm request DTO'larında DataAnnotations validasyonu, CORS (`localhost:4200` için hazır), çakışmalarda (aynı e-posta/grup adı/üyelik) 409 Conflict, süresi dolan refresh token'ların arka plan servisiyle (`RefreshTokenCleanupService`, açılışta ve 6 saatte bir) silinmesi. JWT rol/yetki taşımaz, sadece kimlik (`sub`, e-posta); kimliği doğrulanan her istekte kullanıcının durumu ve rolü veritabanından okunup claim'e o an eklenir (`CurrentUserTokenValidator`): pasifleştirilen kullanıcının token'ı ve rolü değişen kullanıcının eski yetkisi hemen geçersiz olur, token'da eski/güncel olmayan bir rol taşınmaz. Aynı anda gelen çift kayıt veritabanı unique index'iyle yakalanıp 409 döner. Ters proxy arkasında gerçek istemci IP'si için `ForwardedHeaders__Enabled=true` (varsayılan kapalı; açarken güvenilir proxy adresleri tanımlanmalı).

**Bilinen sınırlamalar:** Logout access token'ı anında öldürmez (en fazla 1 saat geçerli kalır; pasifleştirilen kullanıcı hariç). Hesap kilidi sayaçları bellekte tutulur, tek sunuculu çalışma için uygundur ve uygulama yeniden başlayınca sıfırlanır. Her kimlikli istek bir kullanıcı sorgusu daha yapar.

**Sayfalama:** Tüm liste uç noktaları `?page=1&pageSize=20` alır (varsayılan 20, en fazla 100; geçersiz değerler varsayılana çekilir). Cevap biçimi: `{ items, page, pageSize, totalCount, totalPages }`.

**Özellikler (tam CRUD, sahiplik kontrollü):**
- **User:** register/login/refresh/logout/profil (görüntüle+güncelle); Admin: onay, pasifleştirme, rol atama, listeleme (tümü/onay bekleyenler/tekil)
- **Group:** oluşturma/listeleme/tekil görüntüleme/isim güncelleme/silme(soft)/restore, Admin tüm grupları görebilir
- **GroupMember:** üye ekleme/listeleme (mentor ve üyeler görür)/çıkarma(soft)
- **Task:** oluşturma/durum güncelleme/listeleme/tekil görüntüleme/silme(soft)/restore/performans özeti, Admin tüm görevleri görebilir
- **InternshipNote:** ekleme/listeleme/tekil görüntüleme/güncelleme/silme(soft)/restore

**Test:**
- `Backend.Tests`: 225 unit test (xUnit + Moq), servis kuralları ve sahiplik senaryoları; veritabanı gerektirmez.
- `Backend.IntegrationTests`: 107 entegrasyon testi. Uygulamayı gerçek HTTP hattıyla (middleware, JWT, yetki, EF Core, hız sınırı) bellek içinde başlatır; her çalıştırmada **benzersiz adlı geçici bir veritabanı** (`PusulaIT_...`) migration'larla kurulur, bitince silinir (geliştirme veritabanına dokunulmaz). Kapsam: kayıt/giriş/refresh/kilit, rol kapıları ve anlık yetki, parola değiştirme/sıfırlama (e-posta yakalayıcıyla), grup/görev/devir, defter onay akışı, duyurular, arama/sayfalama (joker/SQL enjeksiyonu dahil), hız sınırları ve **migration'ların modelle uyumu** (unutulan migration testi kırar).
  - Varsayılan sunucu LocalDB'dir. Başka bir SQL Server için: `PUSULA_TEST_CONNECTION="Server=localhost,1433;Database={db};User Id=sa;Password=...;TrustServerCertificate=True"` (`{db}` yerine geçici ad konur).
  - Tüm testler: `dotnet test BorBlog.slnx`

**Frontend:** `Frontend/` klasöründe Angular arayüzü var (giriş/kayıt + "beni hatırla", rol bazlı panel, sürükle-bırak görev panosu, onay akışlı staj defteri + PDF çıktı, mentor defter onayları, gruplar, grup duyuruları, admin ekranları, her listede arama; açık/koyu tema, mobil uyumlu). Hiçbir liste "tüm kayıtları çekmez": her şey sunucudan 10'arlı sayfalarla gelir. Çalıştırma: backend `dotnet run`, sonra `cd Frontend && npm install && npm start` (http://localhost:4200). Backend adresi `Frontend/src/app/core/config.ts` içinde.

**Henüz yapılmadı (bilerek sonraya bırakılan):** Docker Compose (Backend+SQL+RabbitMQ birlikte), RabbitMQ (register sonrası email bildirimi), Azure'a canlıya alma.

## 11. Sıradaki Adım (bir sonraki oturum)

1. **AI entegrasyonu** (defter yapılandırılmış olduğu için hazır zemin): günün tamamlanan görevlerinden defter taslağı, metni resmî dile çevirme, haftalık özet, etiketlerden beceri çıkarımı.
2. Bildirimler (yeni görev, duyuru, defter onayı/düzeltme): önce uygulama içi, sonra e-posta (RabbitMQ).
3. Görev yorumları ve dosya eki; mentor haftalık değerlendirmesi (puan/geri bildirim); takvim görünümü; admin raporları ve grafikler.
4. Tarayıcı (arayüz) senaryolarını kalıcı hâle getir (Playwright); şimdilik arayüz akışları geçici betiklerle doğrulandı, repo'da yok. Frontend'de birim test de yok.
5. (Tartışmalı bir davranış) Admin tüm grupları görebiliyor ama bir grubun üyelerini listeleyemiyor ve görev atayamıyor; bilerek böyle bırakıldı.
6. Daha sonra: Docker Compose ile Backend+SQL tek komutla ayağa kaldırma, Azure'a canlıya alma.

**Bilinen sınır:** SQL Server'ın varsayılan karşılaştırma kuralı (`SQL_Latin1_General_CP1_CI_AS`) Türkçe büyük "İ" ile "i"yi eşleştirmez; aramada `İstanbul` yazınca `istanbul` bulunmaz (küçük harfle sorun yok).
