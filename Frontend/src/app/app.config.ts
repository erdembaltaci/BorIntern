import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { AuthService } from './core/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // withComponentInputBinding: rota parametresi (/gruplar/:id) bileşende doğrudan `id = input()` olarak okunur.
    // Sayfa değişince en üste kaydır, "geri" tuşunda eski konuma dön.
    provideRouter(routes, withComponentInputBinding(), withInMemoryScrolling({ scrollPositionRestoration: 'enabled' })),
    // Her HTTP isteği authInterceptor'dan geçer (token ekleme + süresi dolunca yenileme).
    provideHttpClient(withInterceptors([authInterceptor])),
    // Uygulama açılırken kayıtlı oturumun rolünü/adını sunucudan tazeler.
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ],
};
