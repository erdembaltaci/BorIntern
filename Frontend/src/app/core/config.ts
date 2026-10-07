// Backend adresi. Backend `dotnet run` ile (http profili) açılınca 5291 portunu dinler.
// Backend CORS'ta yalnızca http://localhost:4200'e izin verir, bu yüzden `ng serve` varsayılan portunda çalışmalı.
// Canlıya alırken bu değer değiştirilmeli (ileride environment dosyasına taşınabilir).
export const API_URL = 'http://localhost:5291/api';

// Listelerde tek sayfada kaç kayıt gösterileceği (backend en fazla 100'e izin verir).
export const PAGE_SIZE = 10;
