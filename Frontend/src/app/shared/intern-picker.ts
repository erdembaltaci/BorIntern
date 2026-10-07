import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, from, startWith, switchMap } from 'rxjs';
import { User } from '../core/models';
import { initials } from './labels';
import { Icon } from './icon';

export interface PickedIntern {
  id: number;
  name: string;
}

/**
 * Stajyer seçici: seçili kişi çip olarak görünür, "Değiştir" ile arama kutusu açılır (yazdıkça sunucuda aranır,
 * en fazla 8 sonuç gelir; tüm stajyer listesi çekilmez). Arama kaynağı dışarıdan verilir, bu yüzden her rol/ekran
 * kendi yetki kapsamına uygun uç noktayı kullanır.
 * Kullanım:  <app-intern-picker [search]="loadMyInterns" [selected]="assignee()" (picked)="assignee.set($event)" />
 */
@Component({
  selector: 'app-intern-picker',
  imports: [ReactiveFormsModule, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!editing() && selected(); as current) {
      <div class="chosen">
        <span class="avatar">{{ initials(current.name) }}</span>
        <strong class="name">{{ current.name }}</strong>
        <button type="button" class="btn btn-secondary btn-sm" (click)="startEditing()">Değiştir</button>
      </div>
    } @else {
      <div class="search-wrap">
        <input
          class="input"
          type="search"
          autocomplete="off"
          [formControl]="control"
          [placeholder]="placeholder()"
          aria-label="Stajyer ara"
        />
        @if (loading()) {
          <span class="spinner spin"></span>
        }
      </div>
      <div class="results" role="listbox" aria-label="Stajyer arama sonuçları">
        @if (results().length === 0 && !loading()) {
          <p class="muted small empty">Eşleşen stajyer bulunamadı.</p>
        }
        @for (user of results(); track user.id) {
          <button type="button" class="result" role="option" (click)="pick(user)">
            <span class="avatar">{{ initials(user.fullName) }}</span>
            <span class="text">
              <strong>{{ user.fullName }}</strong>
              <span class="muted small">{{ user.email }}</span>
            </span>
            <app-icon name="check" [size]="16" />
          </button>
        }
      </div>
      @if (selected()) {
        <button type="button" class="btn btn-ghost btn-sm cancel" (click)="editing.set(false)">Vazgeç</button>
      }
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .chosen {
      display: flex;
      align-items: center;
      gap: 0.7rem;
      padding: 0.5rem 0.6rem;
      border: 1px solid var(--border-strong);
      border-radius: var(--radius-sm);
      background: var(--surface);
    }
    .name {
      flex: 1;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .search-wrap {
      position: relative;
    }
    .spin {
      position: absolute;
      right: 0.9rem;
      top: 50%;
      margin-top: -9px;
      color: var(--muted);
    }
    .results {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      max-height: 15rem;
      overflow-y: auto;
      padding: 0.35rem;
      border: 1px solid var(--border);
      border-radius: var(--radius-sm);
      background: var(--surface-2);
    }
    .empty {
      padding: 0.5rem;
    }
    .result {
      display: flex;
      align-items: center;
      gap: 0.7rem;
      padding: 0.45rem 0.55rem;
      border: none;
      border-radius: 8px;
      background: var(--surface);
      text-align: left;
      cursor: pointer;
    }
    .result:hover {
      outline: 2px solid var(--primary);
    }
    .text {
      display: flex;
      flex-direction: column;
      flex: 1;
      min-width: 0;
    }
    .text strong,
    .text span {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .cancel {
      align-self: flex-end;
    }
  `,
})
export class InternPicker {
  /** Aranan metne göre stajyer listesi döndüren işlev (boş metinde ilk sonuçlar). */
  readonly search = input.required<(term: string) => Promise<User[]>>();
  readonly selected = input<PickedIntern | null>(null);
  readonly placeholder = input('Stajyerin adını veya e-postasını yaz…');
  readonly picked = output<PickedIntern>();

  protected readonly editing = signal(false);
  protected readonly results = signal<User[]>([]);
  protected readonly loading = signal(false);
  protected readonly control = new FormControl('', { nonNullable: true });
  protected readonly initials = initials;

  constructor() {
    // Yazdıkça 300 ms bekleyip arar; switchMap, eski aramanın geç gelen cevabını yok sayar.
    this.control.valueChanges
      .pipe(
        startWith(''),
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => {
          this.loading.set(true);
          return from(this.search()(term.trim()).catch(() => [] as User[]));
        }),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((users) => {
        this.results.set(users);
        this.loading.set(false);
      });
  }

  protected startEditing(): void {
    this.control.setValue('');
    this.editing.set(true);
  }

  protected pick(user: User): void {
    this.picked.emit({ id: user.id, name: user.fullName });
    this.editing.set(false);
  }
}
