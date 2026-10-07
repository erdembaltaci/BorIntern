import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/theme.service';
import { ConfirmDialog } from './shared/confirm-dialog';
import { ToastContainer } from './shared/toast-container';

/** Kök bileşen: sayfa içeriği (router-outlet) + her sayfada ortak olan bildirim ve onay katmanları. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastContainer, ConfirmDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <router-outlet />
    <app-toast-container />
    <app-confirm-dialog />
  `,
})
export class App {
  // Sadece enjekte etmek yeter: ThemeService oluşurken kayıtlı temayı sayfaya uygular.
  private readonly theme = inject(ThemeService);
}
