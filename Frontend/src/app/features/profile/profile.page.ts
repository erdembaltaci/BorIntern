import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { formatDateTime } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { ToastService } from '../../core/toast.service';
import { Badge } from '../../shared/badge';
import { Icon } from '../../shared/icon';
import { initials } from '../../shared/labels';

/** Kullanıcının kendi bilgilerini gördüğü ve adını güncelleyebildiği sayfa (e-posta, rol ve durum değiştirilemez). */
@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule, Icon, Badge],
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

  protected logout(): void {
    void this.auth.logout();
  }
}
