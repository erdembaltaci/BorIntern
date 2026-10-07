import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { formatDateTime, formatDay, formatDayLong, formatHours } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { Note } from '../../core/models';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { NOTE_STATUS_LABEL } from '../../shared/labels';

/** Backend'in tek seferde döndürdüğü en fazla kayıt (aşılırsa kullanıcıya tarih aralığını daraltması söylenir). */
const EXPORT_LIMIT = 400;

/**
 * Yazdırılabilir staj defteri. Tarayıcının "Yazdır > PDF olarak kaydet" özelliğiyle PDF çıktısı alınır;
 * menü ve kontroller yazdırmada gizlenir (global @media print). Kayıtlar eskiden yeniye sıralıdır.
 */
@Component({
  selector: 'app-journal-print-page',
  imports: [FormsModule, RouterLink, Icon, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header no-print">
        <div>
          <h1>Defteri yazdır</h1>
          <p>Tarih aralığını seç, istersen yalnızca onaylı kayıtları dahil et. Sonra "Yazdır" ile PDF olarak kaydet.</p>
        </div>
        <a routerLink="/defter" class="btn btn-secondary"><app-icon name="arrow-left" [size]="16" /> Deftere dön</a>
      </header>

      <form class="card card-pad controls no-print" (submit)="$event.preventDefault(); load()">
        <div class="field">
          <label for="from">Başlangıç</label>
          <input id="from" class="input" type="date" name="from" [(ngModel)]="fromValue" />
        </div>
        <div class="field">
          <label for="to">Bitiş</label>
          <input id="to" class="input" type="date" name="to" [(ngModel)]="toValue" />
        </div>
        <label class="check">
          <input type="checkbox" name="onlyApproved" [(ngModel)]="onlyApprovedValue" />
          <span>Yalnızca onaylı kayıtlar</span>
        </label>
        <div class="row">
          <button type="submit" class="btn btn-secondary" [disabled]="loading()">Uygula</button>
          <button type="button" class="btn btn-primary" [disabled]="loading() || visible().length === 0" (click)="print()">
            <app-icon name="notes" [size]="18" /> Yazdır / PDF
          </button>
        </div>
      </form>

      @if (error(); as message) {
        <div class="alert alert-error no-print" role="alert">
          <app-icon name="alert-circle" />
          <span>{{ message }}</span>
        </div>
      } @else if (loading()) {
        <div class="skeleton no-print" style="height: 300px"></div>
      } @else if (visible().length === 0) {
        <div class="card no-print">
          <app-empty-state icon="notes" title="Bu ölçütlerde kayıt yok" text="Tarih aralığını genişletmeyi ya da onay filtresini kapatmayı dene." />
        </div>
      } @else {
        @if (notes().length >= limit) {
          <div class="alert alert-warning no-print">
            <app-icon name="alert-circle" />
            <span>En fazla {{ limit }} kayıt gösterilir. Tüm defterin için tarih aralığını bölerek birkaç çıktı al.</span>
          </div>
        }

        <article class="doc">
          <header class="doc-head">
            <div class="doc-brand">Pusula · Staj Defteri</div>
            <h1>{{ user()?.fullName }}</h1>
            <p class="doc-period">{{ period() }}</p>
            <dl class="doc-stats">
              <div><dt>Kayıt</dt><dd>{{ visible().length }}</dd></div>
              <div><dt>Onaylı</dt><dd>{{ approvedCount() }}</dd></div>
              <div><dt>Toplam süre</dt><dd>{{ formatHours(totalHours()) }} saat</dd></div>
            </dl>
          </header>

          @for (note of visible(); track note.id) {
            <section class="doc-entry">
              <div class="doc-entry-head">
                <strong>{{ formatDayLong(note.noteDate) }}</strong>
                @if (note.hoursSpent !== null) {
                  <span>{{ formatHours(note.hoursSpent) }} saat</span>
                }
              </div>
              <h2>{{ note.title || 'Başlıksız kayıt' }}</h2>
              <p class="doc-label">Yapılan iş</p>
              <p class="doc-body">{{ note.content }}</p>
              @if (note.learned) {
                <p class="doc-label">Öğrenilenler</p>
                <p class="doc-body">{{ note.learned }}</p>
              }
              @if (note.tags) {
                <p class="doc-tags">Etiketler: {{ note.tags }}</p>
              }
              <p class="doc-status">
                Durum: {{ statusLabel[note.status] }}
                @if (note.status === 'Approved' && note.reviewedByName) {
                  — {{ note.reviewedByName }}, {{ formatDateTime(note.reviewedAt ?? note.createdAt) }}
                }
              </p>
              @if (note.mentorComment) {
                <p class="doc-comment">Mentor yorumu: {{ note.mentorComment }}</p>
              }
            </section>
          }

          <footer class="doc-sign">
            <div><span>Stajyer</span></div>
            <div><span>Mentor onayı</span></div>
          </footer>
        </article>
      }
    </div>
  `,
  styleUrl: './journal-print.page.css',
})
export class JournalPrintPage implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly user = inject(AuthService).user;

  /** Adres çubuğundan da verilebilir: /defter/yazdir?from=2026-06-01&to=2026-08-31 (withComponentInputBinding). */
  readonly from = input<string>();
  readonly to = input<string>();

  protected fromValue = '';
  protected toValue = '';
  protected onlyApprovedValue = false;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly notes = signal<Note[]>([]);
  private readonly onlyApproved = signal(false);

  protected readonly limit = EXPORT_LIMIT;
  protected readonly statusLabel = NOTE_STATUS_LABEL;
  protected readonly formatDayLong = formatDayLong;
  protected readonly formatHours = formatHours;
  protected readonly formatDateTime = formatDateTime;

  protected readonly visible = computed(() =>
    this.onlyApproved() ? this.notes().filter((n) => n.status === 'Approved') : this.notes(),
  );
  protected readonly approvedCount = computed(() => this.visible().filter((n) => n.status === 'Approved').length);
  protected readonly totalHours = computed(() => this.visible().reduce((sum, n) => sum + (n.hoursSpent ?? 0), 0));
  protected readonly period = computed(() => {
    const list = this.visible();
    if (list.length === 0) return '';
    const first = list[0].noteDate;
    const last = list[list.length - 1].noteDate;
    return first === last ? formatDay(first) : `${formatDay(first)} – ${formatDay(last)}`;
  });

  ngOnInit(): void {
    this.fromValue = this.from() ?? '';
    this.toValue = this.to() ?? '';
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.onlyApproved.set(this.onlyApprovedValue);
      this.notes.set(await this.api.exportNotes(this.fromValue || undefined, this.toValue || undefined));
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  protected print(): void {
    window.print();
  }
}
