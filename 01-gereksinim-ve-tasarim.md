# Adım 1: Gereksinim ve Tasarım

Bu adımın amacı kod yazmadan önce uygulamanın ne yapacağını netleştirmektir.

## 1. Proje Tanımı

**Proje adı:** Staj Takip Sistemi

**Problem:** Stajyerlerin görevleri ve günlük çalışmaları dağınık şekilde takip ediliyor.

**Çözüm:** Stajyerlerin görev ve günlük not girebildiği, yöneticinin kullanıcıları ve çalışmaları takip edebildiği web uygulaması.

**Teknolojiler:**

- Frontend: Angular
- Backend: ASP.NET Core Web API
- Veritabanı: SQL Server / Azure SQL
- Kimlik doğrulama: JWT
- Canlı ortam: Azure

## 2. Kullanıcı Rolleri

### Intern - Stajyer

- Hesap oluşturur.
- Admin onayından sonra giriş yapar.
- Kendi görevlerini görür.
- Görev oluşturur ve durumunu günceller.
- Günlük staj notu ekler.

### Admin - Yönetici

- Admin hesabı public kayıt ekranından oluşturulmaz.
- İlk admin geliştirme sırasında seed ile oluşturulur.
- Stajyerleri listeler.
- Stajyer hesabını onaylar veya pasife alır.
- Tüm görevleri ve staj notlarını görür.

## 3. Kullanıcı Akışları

### Stajyer kayıt akışı

```text
Kayıt formu
    -> Kullanıcı bilgileri doğrulanır
    -> Hesap Pending durumunda oluşturulur
    -> Admin kullanıcıyı görür
    -> Admin onaylar
    -> Stajyer giriş yapabilir
```

### Giriş akışı

```text
Email ve parola
    -> API bilgileri kontrol eder
    -> Bilgiler doğruysa JWT token üretir
    -> Angular token'ı saklar
    -> Token sonraki API isteklerine eklenir
```

### Görev akışı

```text
Stajyer görev formunu doldurur
    -> API kullanıcının giriş yaptığını kontrol eder
    -> Görev veritabanına kaydedilir
    -> Angular görev listesini yeniler
```

## 4. İlk Sürümdeki Veriler

### User

| Alan | Açıklama |
|---|---|
| Id | Kullanıcının benzersiz numarası |
| FullName | Ad ve soyad |
| Email | Giriş email adresi |
| PasswordHash | Hash'lenmiş parola |
| Role | `Admin` veya `Intern` |
| Status | `Pending`, `Active` veya `Passive` |
| CreatedAt | Kayıt tarihi |

### Task

| Alan | Açıklama |
|---|---|
| Id | Görevin benzersiz numarası |
| Title | Görev başlığı |
| Description | Görev açıklaması |
| Status | `Todo`, `InProgress` veya `Completed` |
| DueDate | Son tarih |
| UserId | Görevin sahibi |
| CreatedAt | Oluşturulma tarihi |

### InternshipNote

| Alan | Açıklama |
|---|---|
| Id | Notun benzersiz numarası |
| Content | Günlük not içeriği |
| NoteDate | Not tarihi |
| UserId | Notun sahibi |

## 5. Ekran Listesi

### Herkesin görebileceği ekranlar

- Login
- Register

### Stajyer ekranları

- Dashboard
- Görevlerim
- Yeni görev
- Staj notlarım
- Yeni staj notu

### Admin ekranları

- Admin dashboard
- Stajyerler
- Tüm görevler
- Tüm staj notları

## 6. Yetki Tablosu

| İşlem | Intern | Admin |
|---|---:|---:|
| Kayıt olma | Evet | Hayır |
| Giriş yapma | Evet | Evet |
| Kendi görevlerini görme | Evet | Evet |
| Başka kullanıcının görevini değiştirme | Hayır | Evet |
| Stajyer onaylama | Hayır | Evet |
| Kendi staj notunu ekleme | Evet | İsteğe bağlı |
| Tüm kullanıcıları görme | Hayır | Evet |

## 7. Başarı Kriterleri

Adım 1 tamamlanmış sayılır, eğer:

- Roller ve görevleri belli ise.
- Stajyer kayıt ve admin onay akışı yazılı ise.
- Üç veri modeli ve alanları belirli ise.
- Ekran listesi oluşturulduysa.
- Hangi işlemin hangi role ait olduğu belli ise.
- İlk sürümde yapılmayacak özellikler ayrıldıysa.

## 8. Bu Adımda Yazılmayacak Kodlar

Henüz şunları yazma:

- C# Entity sınıfları
- Controller
- DbContext
- JWT kodu
- Angular component
- CSS
- Azure kaynağı

Bunlar Adım 1 çıktısı netleştikten sonra sırayla yapılacak.

## 9. Adım 2'ye Geçmeden Önce Kontrol

Aşağıdaki soruların cevabı `evet` olmalı:

- Bir stajyer nasıl kayıt oluyor?
- Admin hesabı nasıl oluşuyor?
- Admin onaylamadan stajyer giriş yapabiliyor mu?
- Bir görev kime ait?
- Stajyer başka kullanıcının görevini görebiliyor mu?
- Hangi ekranlar Admin'e özel?

Bu soruların cevabı bu belgede tanımlıdır. Bundan sonraki adımda backend projesi oluşturulacaktır.
