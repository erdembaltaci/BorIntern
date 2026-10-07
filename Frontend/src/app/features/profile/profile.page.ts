import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { formatDateTime } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { PASSWORD_MIN_LENGTH, PASSWORD_PATTERN, passwordsMatch } from '../../core/form.util';
import { ToastService } from '../../core/toast.service';
import { Badge } from '../../shared/badge';
import { Icon } from '../../shared/icon';
import { initials } from '../../shared/labels';
import { PasswordRules } from '../../shared/password-rules';

/** Kullanıcının kendi bilgilerini gördüğü ve adını güncelleyebildiği sayfa (e-posta, rol ve durum değiştirilemez). */
@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule, Icon, Badge, PasswordRules],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile.page.html',
  styleUrl: './profile.page.css',
})
export class ProfilePage {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  protected readonly user = this.auth.user;
  protected readonly saving = signal(false);

  protected readonly nameControl = this.fb.nonNullable.control(this.auth.user()?.fullName ?? '', [
    Validators.required,
    Validators.minLength(2),
  ]);

  // Parola değiştirme
  protected readonly passwordForm = this.fb.nonNullable.group(
    {
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(PASSWORD_MIN_LENGTH), Validators.pattern(PASSWORD_PATTERN)]],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch('newPassword', 'confirmPassword') },
  );
  protected readonly newPasswordValue = toSignal(this.passwordForm.controls.newPassword.valueChanges, { initialValue: '' });
  protected readonly changingPassword = signal(false);
  protected readonly passwordError = signal<string | null>(null);
  protected readonly showPasswords = signal(false);

  protected readonly initials = initials;
  protected readonly formatDateTime = formatDateTime;

  protected async save(): Promise<void> {
    if (this.nameControl.invalid) {
      this.nameControl.markAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      const updated = await this.api.updateMe(this.nameControl.value.trim());
      this.auth.setUser(updated);
      this.nameControl.reset(updated.fullName);
      this.toast.success('Profilin güncellendi.');
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected async changePassword(): Promise<void> {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.changingPassword.set(true);
    this.passwordError.set(null);
    try {
      const { currentPassword, newPassword } = this.passwordForm.getRawValue();
      await this.auth.changePassword(currentPassword, newPassword);
      this.passwordForm.reset({ currentPassword: '', newPassword: '', confirmPassword: '' });
      this.toast.success('Parolan değişti. Diğer cihazlardaki oturumlar kapatıldı.');
    } catch (err) {
      // Örn. "Mevcut parola yanlış." (400), "Çok fazla hatalı deneme..." (429)
      this.passwordError.set(errorMessage(err));
    } finally {
      this.changingPassword.set(false);
    }
  }

  protected logout(): void {
    void this.auth.logout();
  }
}
