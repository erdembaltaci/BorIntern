import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/error.util';
import { PASSWORD_MIN_LENGTH, PASSWORD_PATTERN, passwordsMatch } from '../../core/form.util';
import { ThemeService } from '../../core/theme.service';
import { Icon } from '../../shared/icon';
import { PasswordRules } from '../../shared/password-rules';

/**
 * E-postadaki bağlantıdan (/sifre-sifirla?token=...) gelinen sayfa: yeni parolayı belirler.
 * Anahtar adres çubuğundan `token` girdisi olarak gelir (withComponentInputBinding).
 */
@Component({
  selector: 'app-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink, Icon, PasswordRules],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reset-password.page.html',
  styleUrl: './auth.css',
})
export class ResetPasswordPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  protected readonly themeService = inject(ThemeService);

  readonly token = input<string>();

  protected readonly form = this.fb.nonNullable.group(
    {
      newPassword: ['', [Validators.required, Validators.minLength(PASSWORD_MIN_LENGTH), Validators.pattern(PASSWORD_PATTERN)]],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch('newPassword', 'confirmPassword') },
  );
  protected readonly passwordValue = toSignal(this.form.controls.newPassword.valueChanges, { initialValue: '' });

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);
  protected readonly done = signal(false);

  protected async submit(): Promise<void> {
    const token = this.token();
    if (!token || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      await this.auth.resetPassword(token, this.form.getRawValue().newPassword);
      this.done.set(true);
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }
}
