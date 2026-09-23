# Staj Takip Sistemi

Angular ve ASP.NET Core kullanılarak geliştirilecek basit bir fullstack staj takip uygulaması.

Adım 1 için ayrıntılı çalışma belgesi: [01-gereksinim-ve-tasarim.md](01-gereksinim-ve-tasarim.md)

Onaylanan ekran ve veri tasarımı: [02-ekran-ve-veri-tasarimi.md](02-ekran-ve-veri-tasarimi.md)

Projeyi çalıştırma rehberi: [03-calistirma-ve-gelistirme-akisi.md](03-calistirma-ve-gelistirme-akisi.md)

Katmanlar ve istek akışı: [04-katmanlar-ve-istek-akisi.md](04-katmanlar-ve-istek-akisi.md)

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

- Kullanıcıları onaylama veya pasifleştirme
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
│   ├── Controllers/
│   ├── Data/
│   ├── Entities/
│   ├── Dtos/
│   ├── Services/
│   ├── Migrations/
│   └── Program.cs
├── Frontend/            (henüz oluşturulmadı)
│   └── src/app/
│       ├── core/
│       ├── shared/
│       └── features/
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

Örnek endpoint'ler:

```text
POST /api/auth/register
POST /api/auth/login
GET  /api/tasks
POST /api/tasks
PUT  /api/tasks/{id}
DELETE /api/tasks/{id}
GET  /api/notes
POST /api/notes
GET  /api/admin/users
PUT  /api/admin/users/{id}/approve
```

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

- Git repository ve GitHub bağlantısı kuruldu.
- Docker üzerinde SQL Server container'ı (kalıcı volume ile) çalışıyor.
- Entity'ler, `AppDbContext`, ilk migration oluşturuldu ve veritabanına uygulandı.
- SA parolası/connection string, `.NET User Secrets` ile güvenli şekilde saklanıyor (git'e gitmiyor).
- `Register` ve `Login` endpoint'leri (DTO → Service/Interface → Controller katmanlarıyla) çalışıyor.
- Parola hash'leme `PasswordHasher<User>` ile yapılıyor.
- Swagger UI (`/swagger`) üzerinden endpoint'ler test edilebiliyor (JWT token ile "Authorize" desteği dahil).
- `Login`, `Status != Active` olan kullanıcıları (Pending/Inactive) reddediyor.
- JWT authentication çalışıyor: **sadece `Login`** `AuthResponseDto` (Token + User) döndürüyor. `Register` artık token vermiyor, sadece `UserDto` (güvenlik düzeltmesi — Pending kullanıcı, Register'dan token alıp korumalı endpoint'lere giremesin diye).
- İlk korumalı endpoint: `GET /api/auth/me` (`[Authorize]`), token'daki claim'leri okuyup döndürüyor.
- **N-katmanlı mimariye geçildi:** `Controller → Service → Repository → AppDbContext`. `BaseEntity` (Id, CreatedAt) ve `SoftDeletableEntity : BaseEntity` (IsDeleted, DeletedAt) ile tüm entity'lerdeki tekrar kaldırıldı. `IUserRepository`/`UserRepository` eklendi, `AuthService`/`UserService` artık `AppDbContext`'e değil, repository'ye bağımlı.
- `UserService.ApproveUserAsync` yazıldı (Pending → Active) ve **`AdminController`'a bağlandı**: `POST /api/admin/approve-user/{userId}`, `[Authorize(Roles="Admin")]` ile korumalı — role bazlı yetkilendirme test edildi, çalışıyor.

**Henüz yapılmadı:** Role bazlı yetkilendirmenin diğer yerlere yayılması, global exception middleware (şu an try/catch her controller'da tekrarlanıyor), Group/Task/InternshipNote repository+service+controller'ları, mentor sahiplik kontrolü, soft delete/restore, validasyon, unit testler, **rate limiting / account lockout** (Login endpoint'i şu an brute-force denemelerine karşı korumasız — her istek maliyetsiz kabul ediliyor), Angular frontend.

## 11. Sıradaki Adım (bir sonraki oturum)

1. `Backend.csproj`'daki gereksiz `Microsoft.Extensions.Identity.Core` paket referansını kaldır (`dotnet remove package Microsoft.Extensions.Identity.Core`).
2. Global exception middleware yaz — her controller'da tekrarlanan `try/catch`'i merkezi bir yere topla.
3. **Rate limiting** ekle (`Microsoft.AspNetCore.RateLimiting`) — özellikle `/api/auth/login`'e, brute-force parola denemelerini engellemek için.
4. Ardından sırayla: Group repository/service/controller (mentor grup oluşturma/üye ekleme), Task repository/service/controller (oluşturma/atama/durum güncelleme), InternshipNote repository/service/controller. Her biri, `UserRepository`/`UserService` şablonunu takip edecek.
5. Her yeni endpoint'te mentor/stajyer sahiplik kontrolünü (kendi grubun/görevin mi) uygula.
