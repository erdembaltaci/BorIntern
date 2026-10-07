import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { PAGE_SIZE } from '../../core/config';
import { ConfirmService } from '../../core/confirm.service';
import { formatDay, fromInputDay, isPastDay, toInputDay } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { TaskItem } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { Badge } from '../../shared/badge';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { InternPicker, PickedIntern } from '../../shared/intern-picker';
import { Modal } from '../../shared/modal';
import { Pager } from '../../shared/pager';
import { SearchBox } from '../../shared/search-box';

/**
 * Mentorun atadığı görevlerin listesi: kimin, hangi durumda olduğunu görür; görevi düzenler, başka stajyere devreder
 * ya da siler (geri alınabilir).
 * Stajyerin adı görevle birlikte sunucudan gelir (tek toplu sorgu), ayrıca bir yükleme gerekmez.
 */
@Component({
  selector: 'app-created-tasks-page',
  imports: [ReactiveFormsModule, RouterLink, Icon, Badge, Modal, InternPicker, Pager, EmptyState, SearchBox],
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
                    <div class="row actions">
                      <button type="button" class="btn btn-ghost btn-icon btn-sm" aria-label="Görevi düzenle" title="Düzenle" (click)="openEdit(task)">
                        <app-icon name="edit" />
                      </button>
                      <button type="button" class="btn btn-ghost btn-icon btn-sm" aria-label="Görevi sil" title="Sil" (click)="remove(task)">
                        <app-icon name="trash" />
                      </button>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <app-pager [page]="page()" [totalPages]="totalPages()" (pageChange)="load($event)" />
      }
    </div>

    @if (editing(); as task) {
      <app-modal title="Görevi düzenle" (closed)="closeEdit()">
        <form class="stack" [formGroup]="form" (submit)="$event.preventDefault(); saveEdit()" novalidate>
          <div class="field">
            <label for="editTitle">Başlık</label>
            <input
              id="editTitle"
              class="input"
              type="text"
              formControlName="title"
              maxlength="150"
              [class.invalid]="form.controls.title.touched && form.controls.title.invalid"
            />
            @if (form.controls.title.touched && form.controls.title.invalid) {
              <span class="field-error">Başlık en az 2 karakter olmalı.</span>
            }
          </div>

          <div class="field">
            <label for="editDesc">Açıklama <span class="muted">(isteğe bağlı)</span></label>
            <textarea id="editDesc" class="input" rows="3" formControlName="description" maxlength="2000"></textarea>
          </div>

          <div class="field">
            <label for="editDue">Bitiş tarihi <span class="muted">(isteğe bağlı)</span></label>
            <input id="editDue" class="input" type="date" formControlName="dueDate" />
          </div>

          <div class="field">
            <span class="label">Stajyer</span>
            <app-intern-picker [search]="searchMyInterns" [selected]="assignee()" (picked)="assignee.set($event)" />
            @if (assignee() && assignee()!.id !== task.assignedUserId) {
              <div class="alert alert-warning">
                <app-icon name="alert-circle" />
                <span>Görev {{ assignee()!.name }} adlı stajyere devredilecek ve durumu "Yapılacak"a dönecek.</span>
              </div>
            }
          </div>

          <div class="modal-actions">
            <button type="button" class="btn btn-secondary" (click)="closeEdit()">Vazgeç</button>
            <button type="submit" class="btn btn-primary" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> }
              Kaydet
            </button>
          </div>
        </form>
      </app-modal>
    }
  `,
  styles: `
    .modal-actions {
      display: flex;
      justify-content: flex-end;
      flex-wrap: wrap;
      gap: 0.6rem;
    }
    .actions {
      gap: 0.1rem;
    }
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
  private readonly fb = inject(FormBuilder);

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

  // Düzenleme diyaloğu: null = kapalı, TaskItem = o görev düzenleniyor
  protected readonly editing = signal<TaskItem | null>(null);
  protected readonly saving = signal(false);
  protected readonly assignee = signal<PickedIntern | null>(null);
  protected readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(150)]],
    description: ['', Validators.maxLength(2000)],
    dueDate: [''],
  });

  /** Devir için stajyer arama kaynağı: yalnızca mentorun kendi gruplarındaki stajyerler, en fazla 8 sonuç. */
  protected readonly searchMyInterns = async (term: string) => (await this.api.searchMyInterns(term, 1, 8)).items;

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

  protected openEdit(task: TaskItem): void {
    this.form.reset({ title: task.title, description: task.description, dueDate: toInputDay(task.dueDate) });
    this.assignee.set({ id: task.assignedUserId, name: task.assignedUserName || `Stajyer #${task.assignedUserId}` });
    this.editing.set(task);
  }

  protected closeEdit(): void {
    this.editing.set(null);
  }

  protected async saveEdit(): Promise<void> {
    const task = this.editing();
    const assignee = this.assignee();
    if (this.form.invalid || task === null || assignee === null) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      const value = this.form.getRawValue();
      const reassigned = assignee.id !== task.assignedUserId;
      await this.api.updateTask(task.id, {
        title: value.title.trim(),
        description: value.description.trim(),
        dueDate: fromInputDay(value.dueDate),
        assignedUserId: assignee.id,
      });
      this.toast.success(reassigned ? `Görev ${assignee.name} adlı stajyere devredildi.` : 'Görev güncellendi.');
      this.editing.set(null);
      await this.load(this.page());
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.saving.set(false);
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
