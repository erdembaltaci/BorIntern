import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { API_URL } from './config';

function withToken(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

/**
 * Her giden isteğe `Authorization: Bearer <token>` ekler. Backend 401 dönerse (access token süresi dolmuş olabilir)
 * refresh token ile yeni token alıp isteği BİR KEZ yeniden dener; o da olmazsa oturum kapanır (AuthService.refresh).
 *
 * /auth/ uç noktaları hariç tutulur: yanlış parolada gelen 401'in "oturum doldu" sanılıp refresh döngüsüne girmemesi için.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  const isOurApi = req.url.startsWith(API_URL);
  const isAuthCall = req.url.startsWith(`${API_URL}/auth/`);
  if (!isOurApi) return next(req);

  const token = auth.accessToken();
  const outgoing = token && !isAuthCall ? withToken(req, token) : req;

  return next(outgoing).pipe(
    catchError((err: unknown) => {
      const canRefresh =
        err instanceof HttpErrorResponse && err.status === 401 && !isAuthCall && auth.hasRefreshToken();
      if (!canRefresh) return throwError(() => err);

      return from(auth.refresh()).pipe(
        switchMap(() => next(withToken(req, auth.accessToken()!))),
        // Yenileme başarısızsa asıl (ilk) hatayı döndür; oturumu kapatma işini AuthService zaten yaptı.
        catchError(() => throwError(() => err)),
      );
    }),
  );
};
