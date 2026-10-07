import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/error.util';
import { ThemeService } from '../../core/theme.service';
import { Icon } from '../../shared/icon';

/** "Şifremi unuttum": e-posta ister, kayıtlı olsun olmasın aynı mesajı gösterir (adreslerin kayıtlı olup olmadığı sızmasın). */
@Component({
  selector: 'app-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './forgot-password.page.html',
  styleUrl: './auth.css',
})
export class ForgotPasswordPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  protected readonly themeService = inject(ThemeService);
  /** false: bu kurulumda e-posta gönderimi yok, form yerine yöneticiye yönlendirme gösterilir. */
  protected readonly emailEnabled = this.auth.emailEnabled;

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Gönderildiyse formun yerine bilgilendirme gösterilir. */
  protected readonly sentTo = signal<string | null>(null);

  ngOnInit(): void {
    void this.auth.loadPublicConfig();
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      const email = this.form.getRawValue().email.trim();
      await this.auth.forgotPassword(email);
      this.sentTo.set(email);
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }
}
