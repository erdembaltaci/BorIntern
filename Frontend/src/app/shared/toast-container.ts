import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../core/toast.service';
import { Icon } from './icon';

/** Köşedeki bildirim yığını. App bileşeninde bir kez yerleştirilir. */
@Component({
  selector: 'app-toast-container',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toasts" aria-live="polite">
      @for (toast of toastService.toasts(); track toast.id) {
        <div class="toast" [class]="'toast toast-' + toast.type" role="status">
          <app-icon [name]="toast.type === 'error' ? 'alert-circle' : toast.type === 'info' ? 'info' : 'check-circle'" />
          <span class="msg">{{ toast.message }}</span>
          @if (toast.action) {
            <button type="button" class="action" (click)="run(toast.id, toast.action)">{{ toast.actionLabel }}</button>
          }
          <button type="button" class="close" aria-label="Kapat" (click)="toastService.dismiss(toast.id)">
            <app-icon name="x" [size]="16" />
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .toasts {
      position: fixed;
      right: 1rem;
      bottom: 1rem;
      z-index: 200;
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
      width: min(380px, calc(100vw - 2rem));
      pointer-events: none;
    }
    .toast {
      pointer-events: auto;
      display: flex;
      align-items: center;
      gap: 0.65rem;
      padding: 0.8rem 0.9rem;
      border-radius: var(--radius-sm);
      background: var(--surface);
      border: 1px solid var(--border);
      border-left-width: 4px;
      box-shadow: var(--shadow-md);
      font-size: 0.9rem;
      animation: slide-in 0.25s ease both;
    }
    .toast-success {
      border-left-color: var(--success);
    }
    .toast-success > app-icon {
      color: var(--success);
    }
    .toast-error {
      border-left-color: var(--danger);
    }
    .toast-error > app-icon {
      color: var(--danger);
    }
    .toast-info {
      border-left-color: var(--info);
    }
    .toast-info > app-icon {
      color: var(--info);
    }
    .msg {
      flex: 1;
    }
    .action {
      border: none;
      background: none;
      color: var(--primary);
      font-weight: 700;
      cursor: pointer;
      padding: 0.25rem 0.4rem;
    }
    .close {
      display: grid;
      border: none;
      background: none;
      color: var(--muted);
      cursor: pointer;
      padding: 0.2rem;
    }
    @media (max-width: 640px) {
      .toasts {
        right: 0.75rem;
        left: 0.75rem;
        width: auto;
      }
    }
  `,
})
export class ToastContainer {
  protected readonly toastService = inject(ToastService);

  protected run(id: number, action: () => void): void {
    this.toastService.dismiss(id);
    action();
  }
}
