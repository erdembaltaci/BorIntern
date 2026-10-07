import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map, startWith } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/error.util';
import { ThemeService } from '../../core/theme.service';
import { Icon } from '../../shared/icon';

// Backend'in RegisterRequestDto kuralıyla aynı: en az 8 karakter, bir küçük, bir büyük harf, bir rakam.
// (Sunucu yine de kontrol eder; buradaki kontrol sadece kullanıcıya anında geri bildirim vermek için.)
const PASSWORD_PATTERN = /^(?=.*\p{Ll})(?=.*\p{Lu})(?=.*\d).+$/u;

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password === confirm ? null : { mismatch: true };
}

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.page.html',
  styleUrl: './auth.css',
})
export class RegisterPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  protected readonly themeService = inject(ThemeService);

  protected readonly form = this.fb.nonNullable.group(
    {
      fullName: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8), Validators.pattern(PASSWORD_PATTERN)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);
  /** Kayıt başarılıysa formun yerine "onay bekleniyor" ekranı gösterilir. */
  protected readonly registeredEmail = signal<string | null>(null);

  // Form değerini signal'e çevirip parola kurallarını yazdıkça canlı işaretliyoruz.
  private readonly passwordValue = toSignal(
    this.form.controls.password.valueChanges.pipe(startWith(''), map((v) => v ?? '')),
    { initialValue: '' },
  );
  protected readonly rules = computed(() => {
    const p = this.passwordValue();
    return [
      { label: 'En az 8 karakter', ok: p.length >= 8 },
      { label: 'Bir büyük harf', ok: /\p{Lu}/u.test(p) },
      { label: 'Bir küçük harf', ok: /\p{Ll}/u.test(p) },
      { label: 'Bir rakam', ok: /\d/.test(p) },
    ];
  });

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      const { fullName, email, password } = this.form.getRawValue();
      const user = await this.auth.register(fullName.trim(), email.trim(), password);
      this.registeredEmail.set(user.email);
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }
}
