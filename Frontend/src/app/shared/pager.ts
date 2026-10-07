import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Icon } from './icon';

/** Önceki/sonraki sayfalama. Tek sayfa varsa hiç görünmez. Sayfa numarası değişince `pageChange` yayar. */
@Component({
  selector: 'app-pager',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (totalPages() > 1) {
      <nav class="pager" aria-label="Sayfalama">
        <button type="button" class="btn btn-secondary btn-sm" [disabled]="page() <= 1" (click)="pageChange.emit(page() - 1)">
          <app-icon name="chevron-left" [size]="16" /> Önceki
        </button>
        <span class="muted small">Sayfa {{ page() }} / {{ totalPages() }}</span>
        <button
          type="button"
          class="btn btn-secondary btn-sm"
          [disabled]="page() >= totalPages()"
          (click)="pageChange.emit(page() + 1)"
        >
          Sonraki <app-icon name="chevron-right" [size]="16" />
        </button>
      </nav>
    }
  `,
})
export class Pager {
  readonly page = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly pageChange = output<number>();
}
