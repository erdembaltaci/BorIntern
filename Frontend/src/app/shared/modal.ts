import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, OnInit, inject, input, output, viewChild } from '@angular/core';
import { Icon } from './icon';

/**
 * Ortalanmış diyalog penceresi. Kullanım (açık/kapalı durumunu üst bileşen @if ile yönetir):
 *   @if (open()) { <app-modal title="Yeni grup" (closed)="open.set(false)"> ...form... </app-modal> }
 * Esc tuşu ve dışarı tıklama `closed` olayını tetikler.
 */
@Component({
  selector: 'app-modal',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'closed.emit()' },
  template: `
    <div class="backdrop" (mousedown)="onBackdrop($event)">
      <div #dialog class="dialog" role="dialog" aria-modal="true" [attr.aria-label]="title()" tabindex="-1">
        <header class="dialog-header">
          <h2>{{ title() }}</h2>
          <button type="button" class="btn btn-ghost btn-icon btn-sm" aria-label="Kapat" (click)="closed.emit()">
            <app-icon name="x" />
          </button>
        </header>
        <div class="dialog-body"><ng-content /></div>
      </div>
    </div>
  `,
  styles: `
    .backdrop {
      position: fixed;
      inset: 0;
      z-index: 100;
      display: grid;
      place-items: center;
      padding: 1rem;
      background: rgba(10, 13, 28, 0.55);
      backdrop-filter: blur(3px);
      animation: fade-in 0.15s ease both;
    }
    .dialog {
      width: min(520px, 100%);
      max-height: calc(100dvh - 2rem);
      overflow-y: auto;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      box-shadow: var(--shadow-lg);
      animation: pop-in 0.2s ease both;
    }
    .dialog:focus {
      outline: none;
    }
    .dialog-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 1.1rem 1.25rem;
      border-bottom: 1px solid var(--border);
    }
    .dialog-body {
      padding: 1.25rem;
    }
  `,
})
export class Modal implements OnInit, OnDestroy {
  readonly title = input.required<string>();
  readonly closed = output<void>();

  private readonly dialog = viewChild.required<ElementRef<HTMLElement>>('dialog');

  ngOnInit(): void {
    // Arkadaki sayfa kaymasın; klavye kullanıcısı da doğrudan diyalogun içinde başlasın.
    document.body.style.overflow = 'hidden';
    queueMicrotask(() => {
      const root = this.dialog().nativeElement;
      const firstField = root.querySelector<HTMLElement>('input, textarea, select');
      (firstField ?? root).focus();
    });
  }

  ngOnDestroy(): void {
    document.body.style.overflow = '';
  }

  protected onBackdrop(event: MouseEvent): void {
    // Sadece boşluğa tıklanınca kapat; diyalogun içine tıklama olayı buraya kadar gelse bile hedef farklıdır.
    if (event.target === event.currentTarget) this.closed.emit();
  }
}
