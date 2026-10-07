import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { PAGE_SIZE } from '../../core/config';
import { ConfirmService } from '../../core/confirm.service';
import { formatDay, isPastDay } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { TaskItem } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { Badge } from '../../shared/badge';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { Pager } from '../../shared/pager';
import { SearchBox } from '../../shared/search-box';

/**
 * Mentorun atadığı görevlerin listesi: kimin, hangi durumda olduğunu görür, gerekirse siler (geri alınabilir).
 * Stajyerin adı görevle birlikte sunucudan gelir (tek toplu sorgu), ayrıca bir yükleme gerekmez.
 */
@Component({
  selector: 'app-created-tasks-page',
  imports: [RouterLink, Icon, Badge, Pager, EmptyState, SearchBox],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>Atadığım görevler</h1>
          <p>Stajyerlere atadığın bütün görevler ve güncel durumları. {{ totalCount() }} kayıt.</p>
        </div>
        <a routerLink="/gruplar" class="btn btn-primary"><app-icon name="plus" [size]="18" /> Yeni görev için gruba git</a>
      </header>

      <app-search-box placeholder="Görev başlığı, açıklama veya stajyer adı…" (searched)="onSearch($event)" />

      @if (error(); as message) {
        <div class="alert alert-error" role="alert">
          <app-icon name="alert-circle" />
          <span>{{ message }}</span>
          <button type="button" class="btn btn-secondary btn-sm retry" (click)="load(page())">Tekrar dene</button>
        </div>
      } @else if (loading() && tasks().length === 0) {
        <div class="skeleton" style="height: 280px"></div>
      } @else if (tasks().length === 0 && search()) {
        <div class="card">
          <app-empty-state icon="search" title="Sonuç bulunamadı" [text]="'“' + search() + '” ile eşleşen görev yok.'" />
        </div>
      } @else if (tasks().length === 0) {
        <div class="card">
          <app-empty-state
            icon="tasks"
            title="Henüz görev atamadın"
            text="Bir grubun detayında stajyerinin yanındaki “Görev ata” butonunu kullan."
          >
            <a routerLink="/gruplar" class="btn btn-primary">Gruplarıma git</a>
          </app-empty-state>
        </div>
      } @else {
        <div class="card table-wrap" [class.dim]="loading()">
          <table class="table responsive">
            <thead>
              <tr>
                <th>Görev</th>
                <th>Stajyer</th>
                <th>Durum</th>
                <th>Bitiş</th>
                <th></th>
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
                  <td data-label="Stajyer">{{ task.assignedUserName || 'Stajyer #' + task.assignedUserId }}</td>
                  <td data-label="Durum"><app-badge kind="task" [value]="task.status" /></td>
                  <td data-label="Bitiş" class="nowrap small" [class.overdue]="task.status !== 'Completed' && isPastDay(task.dueDate)">
                    {{ formatDay(task.dueDate) }}
                  </td>
                  <td data-label="İşlem">
                    <button type="button" class="btn btn-ghost btn-icon btn-sm" aria-label="Görevi sil" title="Sil" (click)="remove(task)">
                      <app-icon name="trash" />
                    </button>
                  </td>
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
export class CreatedTasksPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);

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
      const result = await this.api.createdTasks(page, PAGE_SIZE, this.search());
      if (result.items.length === 0 && page > 1) {
        await this.load(page - 1);
        return;
      }
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

  protected async remove(task: TaskItem): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Görevi sil',
      message: `"${task.title}" görevi silinsin mi? Stajyer artık bu görevi göremez. Hemen ardından "Geri al" ile vazgeçebilirsin.`,
      confirmLabel: 'Sil',
      danger: true,
    });
    if (!ok) return;

    try {
      await this.api.deleteTask(task.id);
      this.toast.undoable(`"${task.title}" silindi.`, () => void this.restore(task.id));
      await this.load(this.page());
    } catch (err) {
      this.toast.error(errorMessage(err));
    }
  }

  private async restore(taskId: number): Promise<void> {
    try {
      await this.api.restoreTask(taskId);
      this.toast.success('Görev geri getirildi.');
      await this.load(this.page());
    } catch (err) {
      this.toast.error(errorMessage(err));
    }
  }
}
