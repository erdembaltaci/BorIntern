import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { PAGE_SIZE } from '../../core/config';
import { formatDay, isPastDay } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { TaskItem } from '../../core/models';
import { Badge } from '../../shared/badge';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { Pager } from '../../shared/pager';
import { SearchBox } from '../../shared/search-box';

/** Yönetici: sistemdeki tüm görevleri salt okunur listeler. */
@Component({
  selector: 'app-admin-tasks-page',
  imports: [Icon, Badge, Pager, EmptyState, SearchBox],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>Tüm görevler</h1>
          <p>Mentorların stajyerlere atadığı bütün görevler. {{ totalCount() }} kayıt.</p>
        </div>
      </header>

      <app-search-box placeholder="Başlık, açıklama veya kişi adı…" (searched)="onSearch($event)" />

      @if (error(); as message) {
        <div class="alert alert-error" role="alert">
          <app-icon name="alert-circle" />
          <span>{{ message }}</span>
          <button type="button" class="btn btn-secondary btn-sm retry" (click)="load(page())">Tekrar dene</button>
        </div>
      } @else if (loading() && tasks().length === 0) {
        <div class="skeleton" style="height: 320px"></div>
      } @else if (tasks().length === 0 && search()) {
        <div class="card">
          <app-empty-state icon="search" title="Sonuç bulunamadı" [text]="'“' + search() + '” ile eşleşen görev yok.'" />
        </div>
      } @else if (tasks().length === 0) {
        <div class="card"><app-empty-state icon="tasks" title="Henüz görev yok" text="Mentorlar görev atadığında burada listelenir." /></div>
      } @else {
        <div class="card table-wrap" [class.dim]="loading()">
          <table class="table responsive">
            <thead>
              <tr>
                <th>Görev</th>
                <th>Durum</th>
                <th>Bitiş</th>
                <th>Atanan</th>
                <th>Atayan</th>
              </tr>
            </thead>
            <tbody>
              @for (task of tasks(); track task.id) {
                <tr>
                  <td data-label="Görev">
                    <div class="task-cell">
                      <strong>{{ task.title }}</strong>
                      @if (task.description) {
                        <span class="muted small desc">{{ task.description }}</span>
                      }
                    </div>
                  </td>
                  <td data-label="Durum"><app-badge kind="task" [value]="task.status" /></td>
                  <td data-label="Bitiş" class="nowrap small" [class.overdue]="task.status !== 'Completed' && isPastDay(task.dueDate)">
                    {{ formatDay(task.dueDate) }}
                  </td>
                  <td data-label="Atanan">{{ task.assignedUserName || 'Stajyer #' + task.assignedUserId }}</td>
                  <td data-label="Atayan">{{ task.createdByUserName || 'Mentor #' + task.createdByUserId }}</td>
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
    .task-cell {
      display: flex;
      flex-direction: column;
      min-width: 220px;
    }
    .desc {
      display: -webkit-box;
      -webkit-line-clamp: 2;
      line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
      overflow-wrap: anywhere;
    }
    .overdue {
      color: var(--danger);
      font-weight: 600;
    }
    .dim {
      opacity: 0.55;
      pointer-events: none;
      transition: opacity 0.15s ease;
    }
    .retry {
      margin-left: auto;
    }
    @media (max-width: 720px) {
      .task-cell {
        min-width: 0;
      }
    }
  `,
})
export class AdminTasksPage implements OnInit {
  private readonly api = inject(ApiService);

  /** Arama kutusundaki metin (boş = filtre yok). Değişince liste 1. sayfadan yeniden yüklenir. */
  protected readonly search = signal('');

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly tasks = signal<TaskItem[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly totalCount = signal(0);

  protected readonly formatDay = formatDay;
  protected readonly isPastDay = isPastDay;

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
      const result = await this.api.adminTasks(page, PAGE_SIZE, this.search());
      this.tasks.set(result.items);
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
