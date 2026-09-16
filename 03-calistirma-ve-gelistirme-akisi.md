# Projeyi Çalıştırma ve Geliştirme Akışı

Bu belge, projeyi sıfırdan açıp çalıştırmak için kullanılacak günlük komut akışıdır.

## 1. Proje nerede?

Ana klasör:

```text
C:\Users\sirac\OneDrive\Masaüstü\BorBlog
```

Backend projesi:

```text
C:\Users\sirac\OneDrive\Masaüstü\BorBlog\Backend
```

`.csproj` dosyası .NET projesinin kimliğidir:

```text
Backend\Backend.csproj
```

## 2. Terminali doğru klasörde açma

VS Code'da terminal aç veya PowerShell'de şu komutu çalıştır:

```powershell
cd "C:\Users\sirac\OneDrive\Masaüstü\BorBlog"
```

Doğru yerde olduğunu kontrol et:

```powershell
Get-Location
Get-ChildItem
```

Listede şunları görmelisin:

```text
Backend
README.md
01-gereksinim-ve-tasarim.md
02-ekran-ve-veri-tasarimi.md
```

## 3. İlk kez veya dosya değişikliğinden sonra build

Ana klasörde çalıştır:

```powershell
dotnet build .\Backend\Backend.csproj
```

`build` şu işleri yapar:

1. C# kodlarını derler.
2. NuGet paketlerini kontrol eder.
3. Kod hatalarını gösterir.
4. Çalıştırılabilir çıktı üretir.

Başarılı olursa buna benzer sonuç görürsün:

```text
Backend net10.0 başarılı
```

Build uygulamayı başlatmaz; yalnızca derler ve hata kontrolü yapar.

## 4. Normal çalıştırma komutu

Ana klasörde:

```powershell
dotnet run --project .\Backend\Backend.csproj --launch-profile http
```

Bu komut:

1. Backend projesini bulur.
2. Gerekirse build yapar.
3. `launchSettings.json` içindeki `http` profilini kullanır.
4. API'yi şu adreste başlatır:

```text
http://localhost:5291
```

Terminalde şu mesajı görürsen API çalışıyordur:

```text
Now listening on: http://localhost:5291
```

Terminal açık kaldığı sürece API çalışır. Durdurmak için aynı terminalde:

```text
Ctrl + C
```

## 5. DLL nedir?

`dotnet build` sonrasında .NET derlenmiş kodu DLL dosyasına koyar:

```text
Backend\bin\Debug\net10.0\Backend.dll
```

DLL, doğrudan çift tıklatılan klasik bir program değildir. .NET çalışma ortamı onu `dotnet` komutuyla çalıştırır:

```powershell
dotnet .\Backend\bin\Debug\net10.0\Backend.dll
```

Bu yöntem şunu anlatır:

```text
.NET çalışma ortamı -> Backend.dll dosyasını yükler -> Program.cs çalışır -> API başlar
```

Windows bilgisayarda Uygulama Denetimi politikası `Backend.exe` dosyasını engellerse, yukarıdaki DLL komutunu kullanabilirsin.

Adres kesin olsun istiyorsan:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5291"
dotnet .\Backend\bin\Debug\net10.0\Backend.dll
```

## 6. Çalışan API'yi test etme

API çalışırken ikinci bir terminal aç. Yine ana klasöre geç:

```powershell
cd "C:\Users\sirac\OneDrive\Masaüstü\BorBlog"
```

WeatherForecast endpoint'ini test et:

```powershell
Invoke-RestMethod -Uri "http://localhost:5291/WeatherForecast" -Method Get
```

JSON olarak görmek için:

```powershell
Invoke-RestMethod -Uri "http://localhost:5291/WeatherForecast" -Method Get | ConvertTo-Json -Depth 5
```

Beklenen sonuç HTTP `200 OK` ve hava durumu listesidir.

Tarayıcıdan da şu adresi açabilirsin:

```text
http://localhost:5291/WeatherForecast
```

## 7. Geliştirme sırasında günlük akış

Her özellik için şu sırayı kullan:

```text
1. Dosyada küçük değişiklik yap
2. dotnet build ile derle
3. API'yi çalıştır
4. Endpoint'i test et
5. Sonucu analiz et
6. Bir sonraki küçük değişikliğe geç
```

Örnek:

```powershell
dotnet build .\Backend\Backend.csproj
dotnet run --project .\Backend\Backend.csproj --launch-profile http
```

Başka bir terminalde:

```powershell
Invoke-RestMethod -Uri "http://localhost:5291/WeatherForecast"
```

## 8. Projenin çalışma sırası

```text
Program.cs çalışır
    -> Servisler hazırlanır
    -> Middleware sırası hazırlanır
    -> Controller endpoint'leri bağlanır
    -> API portu dinlemeye başlar
    -> HTTP isteği gelir
    -> İstek ilgili Controller metoduna gider
    -> Controller JSON/HTTP cevabı döndürür
```

Şu anki örnekte:

```text
GET /WeatherForecast
    -> WeatherForecastController.Get()
    -> Bellekte rastgele veri üretir
    -> JSON döndürür
```

İleride:

```text
GET /api/tasks
    -> TasksController
    -> TaskService
    -> DbContext
    -> SQL Server
    -> TaskResponseDto
    -> JSON cevap
```

## 9. İlk aşamalarda ne değişecek?

Şu an:

```text
WeatherForecastController
    -> Rastgele örnek veri
```

Bir sonraki küçük aşama:

```text
TasksController
    -> Geçici bellek listesi
```

Daha sonra:

```text
TasksController
    -> TaskService
    -> Task entity
    -> DbContext
    -> SQL veritabanı
```

JWT ve Angular daha sonra bu akışa eklenecek. Her katmanı çalışır halde görmeden bir sonraki katmana geçilmeyecek.
