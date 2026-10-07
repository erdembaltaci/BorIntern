import { HttpErrorResponse } from '@angular/common/http';

/**
 * Backend'den gelebilecek her hata biçimini kullanıcıya gösterilecek tek bir Türkçe mesaja çevirir:
 *  - { message }        -> ExceptionHandlingMiddleware (404/403/409/400/500)
 *  - { errors: {...} }  -> [ApiController] model doğrulama hatası (400), alan başına mesaj listesi
 *  - gövdesiz 429       -> rate limiter (UseRateLimiter gövde yazmaz)
 *  - status 0           -> sunucuya hiç ulaşılamadı (kapalı / CORS / ağ)
 */
export function errorMessage(err: unknown): string {
  if (!(err instanceof HttpErrorResponse)) {
    return 'Beklenmeyen bir hata oluştu.';
  }

  if (err.status === 0) {
    return 'Sunucuya ulaşılamıyor. Backend çalışıyor mu ve internet bağlantın açık mı?';
  }

  const body = err.error;

  if (body && typeof body === 'object') {
    if (typeof body.message === 'string' && body.message) {
      return body.message;
    }
    if (body.errors && typeof body.errors === 'object') {
      const messages = Object.values(body.errors as Record<string, string[]>).flat();
      if (messages.length) return messages.join(' ');
    }
  }

  switch (err.status) {
    case 401:
      return 'Oturumun geçersiz. Lütfen tekrar giriş yap.';
    case 403:
      return 'Bu işlem için yetkin yok.';
    case 429:
      return 'Çok fazla deneme yaptın. Lütfen bir dakika sonra tekrar dene.';
    default:
      return 'Sunucu hatası oluştu. Lütfen tekrar dene.';
  }
}
