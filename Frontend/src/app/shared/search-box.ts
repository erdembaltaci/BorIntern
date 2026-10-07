import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, map } from 'rxjs';
import { Icon } from './icon';

/**
 * Listelerin üstündeki arama kutusu. Kullanıcı yazmayı bıraktıktan 350 ms sonra `searched` olayını yayar
 * (her tuşta sunucuya istek atmamak için). Boşalınca "" yayar; üst bileşen bunu "filtre yok" sayar.
 * Kullanım:  <app-search-box placeholder="Ara…" (searched)="onSearch($event)" />
 */
@Component({
  selector: 'app-search-box',
  imports: [ReactiveFormsModule, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="search">
      <app-icon name="search" [size]="18" class="lead" />
      <input
        class="input"
        type="text"
        autocomplete="off"
        [formControl]="control"
        [placeholder]="placeholder()"
        [attr.aria-label]="placeholder()"
      />
      @if (control.value) {
        <button type="button" class="btn btn-ghost btn-icon btn-sm clear" aria-label="Aramayı temizle" (click)="clear()">
          <app-icon name="x" [size]="16" />
        </button>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      width: min(420px, 100%);
    }
    .search {
      position: relative;
    }
    .lead {
      position: absolute;
      left: 0.9rem;
      top: 50%;
      transform: translateY(-50%);
      color: var(--muted);
      pointer-events: none;
    }
    .input {
      padding-left: 2.6rem;
      padding-right: 2.6rem;
    }
    .clear {
      position: absolute;
      right: 5px;
      top: 50%;
      transform: translateY(-50%);
    }
  `,
})
export class SearchBox {
  readonly placeholder = input('Ara…');
  readonly searched = output<string>();

  protected readonly control = new FormControl('', { nonNullable: true });

  constructor() {
    this.control.valueChanges
      .pipe(
        debounceTime(350),
        map((value) => value.trim()),
        distinctUntilChanged(),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((term) => this.searched.emit(term));
  }

  protected clear(): void {
    this.control.setValue('');
  }
}
