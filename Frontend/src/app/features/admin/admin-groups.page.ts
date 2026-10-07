import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { PAGE_SIZE } from '../../core/config';
import { formatDateTime } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { Group } from '../../core/models';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { Pager } from '../../shared/pager';
import { SearchBox } from '../../shared/search-box';

/** Yönetici: sistemdeki tüm grupları (hangi mentora ait olduğuyla) salt okunur listeler. */
@Component({
  selector: 'app-admin-groups-page',
  imports: [Icon, Pager, EmptyState, SearchBox],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>Tüm gruplar</h1>
          <p>Sistemdeki bütün mentor grupları. {{ totalCount() }} kayıt.</p>
        </div>
      </header>

      <app-search-box placeholder="Grup veya mentor adı…" (searched)="onSearch($event)" />

      @if (error(); as message) {
        <div class="alert alert-error" role="alert">
          <app-icon name="alert-circle" />
          <span>{{ message }}</span>
          <button type="button" class="btn btn-secondary btn-sm retry" (click)="load(page())">Tekrar dene</button>
        </div>
      } @else if (loading() && groups().length === 0) {
        <div class="skeleton" style="height: 280px"></div>
      } @else if (groups().length === 0 && search()) {
        <div class="card">
          <app-empty-state icon="search" title="Sonuç bulunamadı" [text]="'“' + search() + '” ile eşleşen grup yok.'" />
        </div>
      } @else if (groups().length === 0) {
        <div class="card"><app-empty-state icon="folder" title="Henüz grup yok" text="Mentorlar grup oluşturduğunda burada listelenir." /></div>
      } @else {
        <div class="card table-wrap" [class.dim]="loading()">
          <table class="table responsive">
            <thead>
              <tr>
                <th>Grup</th>
                <th>Mentor</th>
                <th>Oluşturulma</th>
              </tr>
            </thead>
            <tbody>
              @for (group of groups(); track group.id) {
                <tr>
                  <td data-label="Grup"><strong>{{ group.name }}</strong> <span class="muted small">#{{ group.id }}</span></td>
                  <td data-label="Mentor">{{ group.mentorName || 'Mentor #' + group.mentorId }}</td>
                  <td data-label="Oluşturulma" class="nowrap small">{{ formatDateTime(group.createdAt) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <app-pager [page]="page()" [totalPages]="totalPages()" (pageChange)="load($event)" />
      }
    </div>
  `,
  styles: `
    .dim {
      opacity: 0.55;
      pointer-events: none;
      transition: opacity 0.15s ease;
    }
    .retry {
      margin-left: auto;
    }
  `,
})
export class AdminGroupsPage implements OnInit {
  private readonly api = inject(ApiService);

  /** Arama kutusundaki metin (boş = filtre yok). Değişince liste 1. sayfadan yeniden yüklenir. */
  protected readonly search = signal('');

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly groups = signal<Group[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly totalCount = signal(0);

  protected readonly formatDateTime = formatDateTime;

  ngOnInit(): void {
    void this.load(1);
  }

  protected onSearch(term: string): void {
    this.search.set(term);
    void this.load(1);
  }

  protected async load(page: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await this.api.adminGroups(page, PAGE_SIZE, this.search());
      this.groups.set(result.items);
      this.page.set(result.page);
      this.totalPages.set(result.totalPages);
      this.totalCount.set(result.totalCount);
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }
}
