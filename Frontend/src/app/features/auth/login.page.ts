import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/error.util';
import { ThemeService } from '../../core/theme.service';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.page.html',
  styleUrl: './auth.css',
})
export class LoginPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly themeService = inject(ThemeService);

  /** Rota guard'ı giriş yapmamış kullanıcıyı `?returnUrl=...` ile buraya yollar (withComponentInputBinding sayesinde input olur). */
  readonly returnUrl = input<string>();

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
    // İşaretliyse oturum tarayıcı kapansa da açık kalır (AuthService, token'ı localStorage'a yazar).
    rememberMe: [false],
  });

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      const { email, password, rememberMe } = this.form.getRawValue();
      await this.auth.login(email.trim(), password, rememberMe);
      await this.router.navigateByUrl(this.safeReturnUrl());
    } catch (err) {
      this.error.set(this.friendlyError(err));
    } finally {
      this.loading.set(false);
    }
  }

  /** Sadece uygulama içi adreslere dön ("//evil.com" gibi dış adreslere yönlendirme açığı olmasın). */
  private safeReturnUrl(): string {
    const url = this.returnUrl();
    return url && url.startsWith('/') && !url.startsWith('//') ? url : '/';
  }

  /** Backend, onay bekleyen/pasif hesap için sadece "Kullanıcı aktif değil." der; kullanıcıya ne yapacağını söyleyelim. */
  private friendlyError(err: unknown): string {
    const message = errorMessage(err);
    return message.includes('aktif değil')
      ? 'Hesabın henüz yönetici tarafından onaylanmamış ya da pasif durumda. Onaylandıktan sonra giriş yapabilirsin.'
      : message;
  }
}
