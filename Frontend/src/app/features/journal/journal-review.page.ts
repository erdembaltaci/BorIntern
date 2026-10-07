import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { PAGE_SIZE } from '../../core/config';
import { formatDateTime, formatDayLong, formatHours } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { Note, NoteStatus } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { Badge } from '../../shared/badge';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { NOTE_STATUS_LABEL, initials } from '../../shared/labels';
import { Modal } from '../../shared/modal';
import { Pager } from '../../shared/pager';
import { SearchBox } from '../../shared/search-box';

type Filter = 'All' | Exclude<NoteStatus, 'Draft'>;

/** Mentorun, kendi gruplarındaki stajyerlerin gönderdiği defter kayıtlarını incelediği, onayladığı ya da düzeltme istediği sayfa. */
@Component({
  selector: 'app-journal-review-page',
  imports: [ReactiveFormsModule, Icon, Badge, Modal, Pager, EmptyState, SearchBox],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>Defter onayları</h1>
          <p>Stajyerlerinin gönderdiği defter kayıtlarını incele, onayla ya da düzeltme iste.</p>
        </div>
        @if (pendingCount() > 0) {
          <span class="badge badge-info">{{ pendingCount() }} kayıt onay bekliyor</span>
        }
      </header>

      <app-search-box placeholder="Stajyer adı, başlık, iş veya etiket ara…" (searched)="onSearch($event)" />

      <div class="chips" role="tablist" aria-label="Durum filtresi">
        @for (option of filters; track option.value) {
          <button type="button" class="chip" role="tab" [class.active]="filter() === option.value" (click)="setFilter(option.value)">
            {{ option.label }}
          </button>
        }
      </div>

      @if (error(); as message) {
        <div class="alert alert-error" role="alert">
          <app-icon name="alert-circle" />
          <span>{{ message }}</span>
          <button type="button" class="btn btn-secondary btn-sm retry" (click)="load(page())">Tekrar dene</button>
        </div>
      } @else if (loading() && items().length === 0) {
        <div class="stack">
          @for (i of [1, 2, 3]; track i) {
            <div class="skeleton" style="height: 150px"></div>
          }
        </div>
      } @else if (items().length === 0) {
        <div class="card">
          <app-empty-state
            [icon]="search() ? 'search' : 'check-circle'"
            [title]="search() ? 'Sonuç bulunamadı' : filter() === 'Submitted' ? 'Bekleyen kayıt yok' : 'Kayıt yok'"
            [text]="search() ? 'Aramayla eşleşen kayıt yok.' : 'Stajyerlerin defter kaydı gönderdiğinde burada görünecek.'"
          />
        </div>
      } @else {
        <div class="stack" [class.dim]="loading()">
          @for (note of items(); track note.id) {
            <article class="card card-pad entry">
              <div class="entry-head">
                <div class="who">
                  <span class="avatar">{{ initials(note.userName) }}</span>
                  <div class="who-text">
                    <strong>{{ note.userName || 'Stajyer #' + note.userId }}</strong>
                    <span class="muted small">{{ formatDayLong(note.noteDate) }}</span>
                  </div>
                </div>
                <app-badge kind="note" [value]="note.status" />
              </div>

              <h3 class="entry-title">{{ note.title }}</h3>

              <div class="meta">
                @if (note.hoursSpent !== null) {
                  <span class="meta-item"><app-icon name="clock" [size]="14" /> {{ formatHours(note.hoursSpent) }} saat</span>
                }
                @for (tag of tagList(note); track tag) {
                  <span class="tag">{{ tag }}</span>
                }
              </div>

              <div class="text">
                <p class="label small muted">Yapılan iş</p>
                <p class="body">{{ note.content }}</p>
                @if (note.learned) {
                  <p class="label small muted">Öğrenilenler</p>
                  <p class="body">{{ note.learned }}</p>
                }
              </div>

              @if (note.status === 'Submitted') {
                <div class="entry-actions">
                  <button type="button" class="btn btn-primary btn-sm" (click)="openReview(note, true)">
                    <app-icon name="check" [size]="15" /> Onayla
                  </button>
                  <button type="button" class="btn btn-secondary btn-sm" (click)="openReview(note, false)">
                    <app-icon name="undo" [size]="15" /> Düzeltme iste
                  </button>
                  <span class="muted small">Gönderildi: {{ formatDateTime(note.submittedAt ?? note.createdAt) }}</span>
                </div>
              } @else {
                <div class="review" [class.review-warn]="note.status === 'ReturnedForRevision'">
                  <strong>
                    {{ statusLabel[note.status] }}
                    @if (note.reviewedByName) { · {{ note.reviewedByName }} }
                    @if (note.reviewedAt) { · {{ formatDateTime(note.reviewedAt) }} }
                  </strong>
                  @if (note.mentorComment) {
                    <p>{{ note.mentorComment }}</p>
                  }
                </div>
              }
            </article>
          }
        </div>

        <app-pager [page]="page()" [totalPages]="totalPages()" (pageChange)="load($event)" />
      }
    </div>

    @if (target(); as t) {
      <app-modal [title]="t.approve ? 'Kaydı onayla' : 'Düzeltme iste'" (closed)="closeReview()">
        <form class="stack" (submit)="$event.preventDefault(); confirmReview()" novalidate>
          <p class="muted">
            <strong>{{ t.note.userName }}</strong> · {{ formatDayLong(t.note.noteDate) }} · “{{ t.note.title }}”
          </p>
          <div class="field">
            <label for="reviewComment">
              {{ t.approve ? 'Yorum' : 'Stajyer ne düzeltmeli?' }}
              <span class="muted">{{ t.approve ? '(isteğe bağlı)' : '(zorunlu)' }}</span>
            </label>
            <textarea
              id="reviewComment"
              class="input"
              rows="4"
              [formControl]="commentControl"
              maxlength="1000"
              [placeholder]="t.approve ? 'Örn. Güzel iş, süreyi de eklemeyi unutma.' : 'Örn. Yapılan işi daha ayrıntılı yaz ve süreyi ekle.'"
              [class.invalid]="!t.approve && commentControl.touched && !commentControl.value.trim()"
            ></textarea>
            @if (!t.approve && commentControl.touched && !commentControl.value.trim()) {
              <span class="field-error">Düzeltme isterken açıklama yazmalısın.</span>
            }
          </div>
          <div class="modal-actions">
            <button type="button" class="btn btn-secondary" (click)="closeReview()">Vazgeç</button>
            <button type="submit" class="btn" [class.btn-primary]="t.approve" [class.btn-danger]="!t.approve" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> }
              {{ t.approve ? 'Onayla' : 'Düzeltme iste' }}
            </button>
          </div>
        </form>
      </app-modal>
    }
  `,
  styleUrl: './journal.page.css',
  styles: `
    .who {
      display: flex;
      align-items: center;
      gap: 0.7rem;
      min-width: 0;
    }
    .who-text {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
  `,
})
export class JournalReviewPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<Note[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly pendingCount = signal(0);

  protected readonly search = signal('');
  /** Varsayılan olarak mentorun asıl işi olan "onay bekleyenler" gösterilir. */
  protected readonly filter = signal<Filter>('Submitted');

  protected readonly filters: { value: Filter; label: string }[] = [
    { value: 'Submitted', label: 'Onay bekleyen' },
    { value: 'Approved', label: 'Onaylanan' },
    { value: 'ReturnedForRevision', label: 'Düzeltme istenen' },
    { value: 'All', label: 'Tümü' },
  ];

  // Değerlendirme diyaloğu
  protected readonly target = signal<{ note: Note; approve: boolean } | null>(null);
  protected readonly saving = signal(false);
  protected readonly commentControl = this.fb.nonNullable.control('');

  protected readonly statusLabel = NOTE_STATUS_LABEL;
  protected readonly initials = initials;
  protected readonly formatDayLong = formatDayLong;
  protected readonly formatHours = formatHours;
  protected readonly formatDateTime = formatDateTime;

  ngOnInit(): void {
    void this.load(1);
  }

  protected async load(page: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const filter = this.filter();
      // Bekleyen sayısı, hangi sekmede olursak olalım güncel kalsın diye 1 kayıtlık ayrı bir istekle alınır.
      const [result, pending] = await Promise.all([
        this.api.reviewQueue(page, PAGE_SIZE, filter === 'All' ? undefined : filter, this.search()),
        this.api.reviewQueue(1, 1, 'Submitted'),
      ]);
      if (result.items.length === 0 && page > 1) {
        await this.load(page - 1);
        return;
      }
      this.items.set(result.items);
      this.page.set(result.page);
      this.totalPages.set(result.totalPages);
      this.pendingCount.set(pending.totalCount);
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  protected onSearch(term: string): void {
    this.search.set(term);
    void this.load(1);
  }

  protected setFilter(filter: Filter): void {
    this.filter.set(filter);
    void this.load(1);
  }

  protected tagList(note: Note): string[] {
    return note.tags ? note.tags.split(',').map((t) => t.trim()).filter(Boolean) : [];
  }

  protected openReview(note: Note, approve: boolean): void {
    this.commentControl.reset('');
    this.target.set({ note, approve });
  }

  protected closeReview(): void {
    this.target.set(null);
  }

  protected async confirmReview(): Promise<void> {
    const t = this.target();
    if (!t) return;

    const comment = this.commentControl.value.trim();
    if (!t.approve && !comment) {
      this.commentControl.markAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      await this.api.reviewNote(t.note.id, t.approve, comment);
      this.toast.success(t.approve ? 'Kayıt onaylandı.' : 'Düzeltme isteği stajyere iletildi.');
      this.target.set(null);
      await this.load(this.page());
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.saving.set(false);
    }
  }
}
