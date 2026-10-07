import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ConfirmService } from '../core/confirm.service';
import { Modal } from './modal';

/** ConfirmService'in ekrandaki yüzü. App bileşeninde bir kez yerleştirilir. */
@Component({
  selector: 'app-confirm-dialog',
  imports: [Modal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (confirm.pending(); as request) {
      <app-modal [title]="request.title" (closed)="confirm.answer(false)">
        <p class="message">{{ request.message }}</p>
        <div class="actions">
          <button type="button" class="btn btn-secondary" (click)="confirm.answer(false)">Vazgeç</button>
          <button
            type="button"
            class="btn"
            [class.btn-danger]="request.danger"
            [class.btn-primary]="!request.danger"
            (click)="confirm.answer(true)"
          >
            {{ request.confirmLabel ?? 'Onayla' }}
          </button>
        </div>
      </app-modal>
    }
  `,
  styles: `
    .message {
      color: var(--muted);
      margin-bottom: 1.25rem;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
      flex-wrap: wrap;
      gap: 0.6rem;
    }
  `,
})
export class ConfirmDialog {
  protected readonly confirm = inject(ConfirmService);
}
