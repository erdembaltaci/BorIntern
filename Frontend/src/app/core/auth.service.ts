import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_URL } from './config';
import { AuthResponse, Role, User } from './models';
import { ToastService } from './toast.service';

const ACCESS_KEY = 'auth.accessToken';
const REFRESH_KEY = 'auth.refreshToken';
const USER_KEY = 'auth.user';

// Storage erişimi (gizli mod, kurumsal politika vb. yüzünden) hata verebilir; uygulama çökmesin diye her erişim korunur.
function getStorage(kind: 'local' | 'session'): Storage | null {
  try {
    return kind === 'local' ? localStorage : sessionStorage;
  } catch {
    return null;
  }
}

function readFrom(storage: Storage | null, key: string): string | null {
  try {
    return storage?.getItem(key) ?? null;
  } catch {
    return null;
  }
}

function writeTo(storage: Storage | null, key: string, value: string | null): void {
  try {
    if (value === null) storage?.removeItem(key);
    else storage?.setItem(key, value);
  } catch {
    /* yoksay */
  }
}

/**
 * Oturum yönetimi: giriş, kayıt, çıkış ve token yenileme.
 *
 * "Beni hatırla" (rememberMe):
 *  - İşaretli  -> token'lar localStorage'da: tarayıcı kapansa da oturum açık kalır.
 *  - İşaretsiz -> token'lar sessionStorage'da: sekme/tarayıcı kapanınca oturum biter (ortak bilgisayarlar için güvenli).
 *
 * Bilinen bedel: token'lar tarayıcı deposunda durduğu için sayfaya zararlı bir script (XSS) girerse okunabilir.
 * Angular şablonları çıktıyı varsayılan olarak temizlediği için risk düşük; daha sıkı güvenlik için ileride
 * token'ı HttpOnly cookie'ye taşımak gerekir.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  private readonly local = getStorage('local');
  private readonly session = getStorage('session');

  /**
   * Oturum şu an hangi depoda duruyor? Açılışta token'ın bulunduğu depo seçilir; token yenilenince
   * (refresh) yeni değerler yine AYNI depoya yazılır ki "hatırlama" tercihi bozulmasın.
   */
  private persistent = readFrom(this.local, ACCESS_KEY) !== null;

  private readonly access = signal<string | null>(this.readStored(ACCESS_KEY));
  private readonly refreshTokenValue = signal<string | null>(this.readStored(REFRESH_KEY));

  readonly user = signal<User | null>(this.readStoredUser());
  readonly isLoggedIn = computed(() => this.user() !== null && this.access() !== null);
  readonly role = computed<Role | null>(() => this.user()?.role ?? null);

  /** Aynı anda gelen birden fazla 401 için TEK refresh isteği atılsın diye saklanır (refresh token tek kullanımlık). */
  private refreshing: Promise<void> | null = null;

  accessToken(): string | null {
    return this.access();
  }

  hasRefreshToken(): boolean {
    return this.refreshTokenValue() !== null;
  }

  async login(email: string, password: string, rememberMe = false): Promise<User> {
    const res = await firstValueFrom(this.http.post<AuthResponse>(`${API_URL}/auth/login`, { email, password }));
    // Eski oturumdan kalan değerler diğer depoda asılı kalmasın.
    this.wipeStorages();
    this.persistent = rememberMe;
    this.saveSession(res);
    return res.user;
  }

  /** Kayıt token döndürmez: hesap Admin onaylayana kadar Pending kalır. */
  register(fullName: string, email: string, password: string): Promise<User> {
    return firstValueFrom(this.http.post<User>(`${API_URL}/auth/register`, { fullName, email, password }));
  }

  /** Çıkışta sunucudaki refresh token da iptal edilir. Sunucuya ulaşılamasa bile yerel oturum her halükârda silinir. */
  async logout(): Promise<void> {
    const refreshToken = this.refreshTokenValue();
    this.clearSession();
    await this.router.navigate(['/giris']);
    if (refreshToken) {
      try {
        await firstValueFrom(this.http.post(`${API_URL}/auth/logout`, { refreshToken }));
      } catch {
        /* token zaten geçersiz ya da sunucu kapalı: kullanıcı açısından çıkış tamamlandı */
      }
    }
  }

  /**
   * Refresh token ile yeni token çifti alır. Sunucu refresh token'ı reddederse (süresi dolmuş/iptal)
   * oturum kapatılır; sadece ağ hatasıysa oturum korunur ki internet gelince devam edilebilsin.
   */
  refresh(): Promise<void> {
    if (this.refreshing) return this.refreshing;

    const refreshToken = this.refreshTokenValue();
    if (!refreshToken) {
      return Promise.reject(new Error('Refresh token yok.'));
    }

    this.refreshing = firstValueFrom(this.http.post<AuthResponse>(`${API_URL}/auth/refresh`, { refreshToken }))
      .then((res) => this.saveSession(res))
      .catch((err: unknown) => {
        if (!(err instanceof HttpErrorResponse) || err.status !== 0) {
          this.expireSession();
        }
        throw err;
      })
      .finally(() => {
        this.refreshing = null;
      });

    return this.refreshing;
  }

  /** Uygulama açılırken: kayıtlı oturum varsa rol/ad güncel mi diye sunucudan tazeler (rol değişmiş olabilir). */
  async restoreSession(): Promise<void> {
    if (!this.isLoggedIn()) return;
    try {
      const me = await firstValueFrom(this.http.get<User>(`${API_URL}/users/me`));
      this.setUser(me);
    } catch {
      /* 401'i interceptor halleder; ağ hatasında önbellekteki kullanıcıyla devam edilir */
    }
  }

  setUser(user: User): void {
    this.user.set(user);
    this.write(USER_KEY, JSON.stringify(user));
  }

  /** Oturum süresi doldu / iptal edildi: kullanıcıyı bilgilendirip girişe yönlendirir. */
  expireSession(): void {
    if (!this.isLoggedIn() && !this.hasRefreshToken()) return;
    this.clearSession();
    this.toast.info('Oturum süren doldu, lütfen tekrar giriş yap.');
    void this.router.navigate(['/giris']);
  }

  private saveSession(res: AuthResponse): void {
    this.access.set(res.token);
    this.refreshTokenValue.set(res.refreshToken);
    this.write(ACCESS_KEY, res.token);
    this.write(REFRESH_KEY, res.refreshToken);
    this.setUser(res.user);
  }

  private clearSession(): void {
    this.access.set(null);
    this.refreshTokenValue.set(null);
    this.user.set(null);
    this.wipeStorages();
  }

  /** Aktif depoya yazar, diğer depodaki eski kopyayı siler. */
  private write(key: string, value: string | null): void {
    writeTo(this.persistent ? this.local : this.session, key, value);
    writeTo(this.persistent ? this.session : this.local, key, null);
  }

  private wipeStorages(): void {
    for (const key of [ACCESS_KEY, REFRESH_KEY, USER_KEY]) {
      writeTo(this.local, key, null);
      writeTo(this.session, key, null);
    }
  }

  private readStored(key: string): string | null {
    return readFrom(this.local, key) ?? readFrom(this.session, key);
  }

  private readStoredUser(): User | null {
    try {
      const raw = this.readStored(USER_KEY);
      return raw ? (JSON.parse(raw) as User) : null;
    } catch {
      return null;
    }
  }
}
