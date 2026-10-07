import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { Role } from './models';

/** Giriş yapmamış kullanıcıyı giriş sayfasına gönderir; girişten sonra gitmek istediği sayfaya dönebilsin diye adresi saklar. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn() ? true : router.createUrlTree(['/giris'], { queryParams: { returnUrl: state.url } });
};

/** Giriş yapmış kullanıcının giriş/kayıt sayfalarını görmesine gerek yok, panele yönlendirilir. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn() ? router.createUrlTree(['/']) : true;
};

/**
 * Sayfayı sadece belirli rollere açar. Bu yalnızca KULLANICI DENEYİMİ içindir (yetkisiz kişi boş sayfa görmesin);
 * asıl güvenlik backend'deki [Authorize(Roles=...)] ve servis kontrollerindedir.
 */
export const roleGuard =
  (...roles: Role[]): CanActivateFn =>
  () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    const role = auth.role();
    return role && roles.includes(role) ? true : router.createUrlTree(['/']);
  };
