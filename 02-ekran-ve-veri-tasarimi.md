# Adım 2: Ekran ve Veri Tasarımı

Bu belge, kodlamaya başlamadan önce onaylanan kullanıcı akışını ve veri ilişkilerini tanımlar.

## 1. Kesinleşen İş Akışı

```text
Stajyer kayıt olur
    -> Hesap Pending durumunda açılır
    -> Admin stajyeri görür ve onaylar
    -> Stajyer giriş yapar
    -> Admin stajyere görev atar
    -> Stajyer görevi görür ve durumunu günceller
    -> Stajyer günlük not ekler
    -> Admin ilerlemeyi takip eder
```

## 2. Ekranlar

### Public ekranlar

#### Login

Amaç: Kullanıcının email ve parola ile giriş yapması.

Alanlar:

- Email
- Parola
- Giriş yap butonu
- Kayıt ol bağlantısı

Durumlar:

- Hatalı email veya parola
- Pending durumundaki kullanıcı
- Pasif kullanıcı
- Başarılı giriş

#### Register

Amaç: Stajyer hesabı oluşturmak.

Alanlar:

- Ad soyad
- Email
- Parola
- Parola tekrar
- Kayıt ol butonu

Başarılı kayıt sonrası kullanıcıya hesabının admin onayı beklediği gösterilir.

### Intern ekranları

#### Dashboard

Gösterilecek özetler:

- Toplam görev
- Bekleyen görev
- Devam eden görev
- Tamamlanan görev
- Son staj notları

#### Görevlerim

- Kendisine atanan görevleri listeler.
- Duruma göre filtreler.
- Görev detayını görür.
- Görev durumunu günceller.

Stajyer görev oluşturamaz ve görev atamasını değiştiremez.

#### Staj Notlarım

- Günlük notları listeler.
- Yeni not ekler.
- Kendi notunu düzenler veya siler.

### Admin ekranları

#### Admin Dashboard

Gösterilecek özetler:

- Bekleyen stajyer sayısı
- Aktif stajyer sayısı
- Toplam görev sayısı
- Tamamlanan görev sayısı

#### Stajyer Yönetimi

- Stajyerleri listeler.
- Pending stajyeri onaylar.
- Kullanıcıyı pasife alır.
- Stajyerin görevlerini görüntüler.

#### Görev Yönetimi

- Görev oluşturur.
- Görevi bir stajyere atar.
- Görevleri listeler.
- Görev detayını ve durumunu görür.

Yeni görev formu:

- Başlık
- Açıklama
- Atanacak stajyer
- Son tarih

#### Tüm Staj Notları

- Stajyer notlarını listeler.
- Stajyer ve tarih ile filtreler.
- Notları yalnızca görüntüler.

## 3. Veritabanı İlişkileri

```text
User 1 ---- * Task
User 1 ---- * InternshipNote
```

- Bir stajyerin birçok görevi olabilir.
- Bir stajyerin birçok staj notu olabilir.
- Her görev bir stajyere atanır.
- Her staj notu bir kullanıcıya aittir.

## 4. Entity Taslağı

### User

```text
Id: int
FullName: string
Email: string
PasswordHash: string
Role: string
Status: string
CreatedAt: DateTime
Tasks: collection
InternshipNotes: collection
```

### Task

```text
Id: int
Title: string
Description: string
Status: string
DueDate: DateTime?
AssignedUserId: int
CreatedAt: DateTime
AssignedUser: User
```

### InternshipNote

```text
Id: int
Content: string
NoteDate: DateTime
UserId: int
User: User
```

## 5. API Taslağı

### Authentication

```text
POST /api/auth/register
POST /api/auth/login
```

### Admin

```text
GET  /api/admin/users
PUT  /api/admin/users/{id}/approve
PUT  /api/admin/users/{id}/deactivate
GET  /api/admin/tasks
POST /api/admin/tasks
GET  /api/admin/notes
```

### Intern

```text
GET  /api/tasks/my
PUT  /api/tasks/{id}/status
GET  /api/notes/my
POST /api/notes
PUT  /api/notes/{id}
DELETE /api/notes/{id}
```

## 6. Uygulama Tasarım Kararı

İlk frontend sürümünde:

- Angular Material kullanılacak.
- Masaüstünde sol menü, mobilde açılır menü olacak.
- Admin ve Intern için farklı dashboard içerikleri gösterilecek.
- Form gönderilirken loading durumu gösterilecek.
- Başarılı işlemler snackbar ile bildirilecek.
- API hataları kullanıcıya anlaşılır mesajla gösterilecek.

## 7. Kodlamaya Geçiş Sırası

Bu tasarım onaylandıktan sonra:

1. Backend klasörü ve ASP.NET Core Web API oluşturulacak.
2. `User`, `Task` ve `InternshipNote` entity sınıfları yazılacak.
3. `DbContext` ve veritabanı ilişkileri kurulacak.
4. DTO sınıfları belirlenecek.
5. Migration oluşturulacak.
6. Authentication eklenecek.

Bir sonraki adımda ilk kod olarak backend projesi oluşturulacak; önce sadece çalışan boş API ve Swagger kontrol edilecek.
