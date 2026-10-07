import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon } from './icon';

/** Liste boşken gösterilen yönlendirici kutu. Altına buton eklemek için içerik yansıtılır (<ng-content>). */
@Component({
  selector: 'app-empty-state',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty">
      <div class="empty-icon"><app-icon [name]="icon()" [size]="26" /></div>
      <h3>{{ title() }}</h3>
      @if (text()) {
        <p class="muted">{{ text() }}</p>
      }
      <ng-content />
    </div>
  `,
  styles: `
    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      text-align: center;
      padding: 2.5rem 1rem;
    }
    .empty p {
      max-width: 38ch;
    }
    .empty-icon {
      display: grid;
      place-items: center;
      width: 56px;
      height: 56px;
      border-radius: 50%;
      background: var(--primary-soft);
      color: var(--primary);
      margin-bottom: 0.25rem;
    }
    :host ::ng-deep .empty .btn {
      margin-top: 0.75rem;
    }
  `,
})
export class EmptyState {
  readonly icon = input('inbox');
  readonly title = input.required<string>();
  readonly text = input('');
}
