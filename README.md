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

- `Admin`: Yönetici
- `Intern`: Stajyer

### Hesap oluşturma akışı

1. Admin hesabı başlangıçta seed ile oluşturulur.
2. Stajyer kayıt ekranından hesap oluşturur.
3. Yeni stajyerin durumu `Pending` olur.
4. Admin stajyeri onaylar.
5. Onaylanan stajyer sisteme giriş yapabilir.

Kullanıcı alanları:

```text
Id, FullName, Email, PasswordHash, Role, Status, CreatedAt
```

İlk sürümde `Mentor`, `HR` veya `SuperAdmin` gibi ek roller yapılmayacak.

## 3. Uygulamanın Temel Özellikleri

### Stajyer

- Kayıt olma ve giriş yapma
- Kendi görevlerini görüntüleme
- Görev oluşturma ve durum güncelleme
- Günlük staj notu ekleme
- Kendi profilini görüntüleme

### Admin

- Stajyerleri listeleme
- Stajyer onaylama veya pasife alma
- Tüm görevleri görüntüleme
- Görev ve not kayıtlarını inceleme

## 4. Veri Modelleri

### User

- `Id`
- `FullName`
- `Email`
- `PasswordHash`
- `Role`
- `Status`
- `CreatedAt`

### Task

- `Id`
- `Title`
- `Description`
- `Status`
- `DueDate`
- `UserId`
- `CreatedAt`

### InternshipNote

- `Id`
- `Content`
- `NoteDate`
- `UserId`

İlişki: Bir kullanıcı birçok görev ve staj notuna sahip olabilir.

## 5. Proje Yapısı

```text
BorBlog/
├── Backend/
│   ├── Controllers/
│   ├── Data/
│   ├── Entities/
│   ├── DTOs/
│   ├── Services/
│   └── Program.cs
├── Frontend/
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
- Docker
- Redis
- SignalR
- Çok seviyeli rol sistemi
- Gelişmiş raporlama
- Dosya yükleme

Önce küçük ama uçtan uca çalışan sistemi tamamla; daha sonra özellik ekle.
