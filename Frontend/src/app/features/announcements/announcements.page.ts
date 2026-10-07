import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { PAGE_SIZE } from '../../core/config';
import { formatDateTime } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { Announcement } from '../../core/models';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { Pager } from '../../shared/pager';

/** Stajyerin, üyesi olduğu grupların mentorlarından gelen duyuruları (en yeni önce) okuduğu sayfa. */
@Component({
  selector: 'app-announcements-page',
  imports: [Icon, Pager, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>Duyurular</h1>
          <p>Mentorlarının gruplarına gönderdiği duyurular.</p>
        </div>
        @if (totalCount() > 0) {
          <span class="badge badge-primary">{{ totalCount() }} duyuru</span>
        }
      </header>

      @if (error(); as message) {
        <div class="alert alert-error" role="alert">
          <app-icon name="alert-circle" />
          <span>{{ message }}</span>
          <button type="button" class="btn btn-secondary btn-sm retry" (click)="load(page())">Tekrar dene</button>
        </div>
      } @else if (loading() && items().length === 0) {
        <div class="stack">
          @for (i of [1, 2, 3]; track i) {
            <div class="skeleton" style="height: 120px"></div>
          }
        </div>
      } @else if (items().length === 0) {
        <div class="card">
          <app-empty-state
            icon="megaphone"
            title="Henüz duyuru yok"
            text="Mentorun grubuna bir duyuru gönderdiğinde burada görünecek."
          />
        </div>
      } @else {
        <div class="stack" [class.dim]="loading()">
          @for (item of items(); track item.id) {
            <article class="card card-pad item">
              <div class="row-between">
                <span class="badge badge-primary">{{ item.groupName || 'Grup #' + item.groupId }}</span>
                <span class="muted small">{{ formatDateTime(item.createdAt) }}</span>
              </div>
              <h3>{{ item.title }}</h3>
              <p class="content">{{ item.content }}</p>
              <span class="muted small from"><app-icon name="user" [size]="14" /> {{ item.mentorName || 'Mentor' }}</span>
            </article>
          }
        </div>
        <app-pager [page]="page()" [totalPages]="totalPages()" (pageChange)="load($event)" />
      }
    </div>
  `,
  styles: `
    .item {
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
    }
    .item h3 {
      overflow-wrap: anywhere;
    }
    .content {
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }
    .from {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }
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
export class AnnouncementsPage implements OnInit {
  private readonly api = inject(ApiService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<Announcement[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly totalCount = signal(0);

  protected readonly formatDateTime = formatDateTime;

  ngOnInit(): void {
    void this.load(1);
  }

  protected async load(page: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await this.api.myAnnouncements(page, PAGE_SIZE);
      this.items.set(result.items);
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
