import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PASSWORD_MIN_LENGTH } from '../core/form.util';
import { Icon } from './icon';

/** Yazdıkça işaretlenen parola kural listesi. Kullanım: <app-password-rules [value]="parolaMetni()" /> */
@Component({
  selector: 'app-password-rules',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul class="rules" aria-label="Parola kuralları">
      @for (rule of rules(); track rule.label) {
        <li [class.ok]="rule.ok">
          <span class="tick">@if (rule.ok) { <app-icon name="check" [size]="10" /> }</span>
          {{ rule.label }}
        </li>
      }
    </ul>
  `,
  styles: `
    .rules {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.35rem 0.75rem;
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: 0.82rem;
      color: var(--muted);
    }
    .rules li {
      display: flex;
      align-items: center;
      gap: 0.4rem;
    }
    .rules li.ok {
      color: var(--success);
    }
    .tick {
      display: grid;
      place-items: center;
      width: 16px;
      height: 16px;
      border-radius: 50%;
      border: 1.5px solid currentColor;
      flex: none;
    }
    @media (max-width: 420px) {
      .rules {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class PasswordRules {
  readonly value = input.required<string>();

  protected readonly rules = computed(() => {
    const p = this.value();
    return [
      { label: `En az ${PASSWORD_MIN_LENGTH} karakter`, ok: p.length >= PASSWORD_MIN_LENGTH },
      { label: 'Bir büyük harf', ok: /\p{Lu}/u.test(p) },
      { label: 'Bir küçük harf', ok: /\p{Ll}/u.test(p) },
      { label: 'Bir rakam', ok: /\d/.test(p) },
    ];
  });
}
