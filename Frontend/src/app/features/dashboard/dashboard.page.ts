import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { formatDateTime, formatDay, isPastDay, relativeDay } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { Announcement, Group, TaskItem, TaskSummary, User } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { Badge } from '../../shared/badge';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { initials } from '../../shared/labels';

interface AdminStats {
  users: number;
  pending: number;
  groups: number;
  tasks: number;
}

/**
 * Giriş sonrası ilk ekran. İçerik role göre değişir:
 *  - Stajyer: görev ilerlemesi + yaklaşan teslimler
 *  - Mentor:  grupları
 *  - Admin:   sistem sayıları + onay bekleyen kullanıcılar
 */
@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, Icon, Badge, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.css',
})
export class DashboardPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  protected readonly user = inject(AuthService).user;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // Stajyer
  protected readonly summary = signal<TaskSummary>({ totalTasks: 0, todoCount: 0, inProgressCount: 0, completedCount: 0, overdueCount: 0 });
  protected readonly upcoming = signal<TaskItem[]>([]);
  protected readonly noteCount = signal(0);
  /** Mentorun "düzeltme istedi" dediği defter kayıtları (stajyerin dikkat etmesi gereken). */
  protected readonly returnedNoteCount = signal(0);
  protected readonly latestAnnouncements = signal<Announcement[]>([]);
  // Mentor
  protected readonly groups = signal<Group[]>([]);
  protected readonly groupTotal = signal(0);
  protected readonly createdTaskTotal = signal(0);
  /** Mentorun onayını bekleyen defter kayıtları. */
  protected readonly pendingJournalCount = signal(0);
  // Admin
  protected readonly stats = signal<AdminStats>({ users: 0, pending: 0, groups: 0, tasks: 0 });
  protected readonly pendingUsers = signal<User[]>([]);
  protected readonly approvingId = signal<number | null>(null);

  protected readonly greeting = computed(() => {
    const hour = new Date().getHours();
    const part = hour < 6 ? 'İyi geceler' : hour < 12 ? 'Günaydın' : hour < 18 ? 'İyi günler' : 'İyi akşamlar';
    const firstName = this.user()?.fullName.trim().split(/\s+/)[0] ?? '';
    return `${part}, ${firstName}`;
  });

  // Stajyer istatistikleri: sunucudan hazır sayı olarak gelir (görevlerin kendisi çekilmez).
  protected readonly todoCount = computed(() => this.summary().todoCount);
  protected readonly inProgressCount = computed(() => this.summary().inProgressCount);
  protected readonly completedCount = computed(() => this.summary().completedCount);
  protected readonly overdueCount = computed(() => this.summary().overdueCount);
  protected readonly completionPercent = computed(() => {
    const { totalTasks, completedCount } = this.summary();
    return totalTasks === 0 ? 0 : Math.round((completedCount / totalTasks) * 100);
  });

  protected readonly formatDay = formatDay;
  protected readonly formatDateTime = formatDateTime;
  protected readonly relativeDay = relativeDay;
  protected readonly isPastDay = isPastDay;
  protected readonly initials = initials;

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      switch (this.user()?.role) {
        case 'Intern':
          await this.loadIntern();
          break;
        case 'Mentor':
          await this.loadMentor();
          break;
        case 'Admin':
          await this.loadAdmin();
          break;
      }
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  private async loadIntern(): Promise<void> {
    // Hepsi küçük istekler: sayılar + en yakın 5 görev + not sayısı + son 3 duyuru (tüm görevler çekilmez).
    const [summary, upcoming, notes, returned, announcements] = await Promise.all([
      this.api.mySummary(),
      this.api.myUpcoming(5),
      this.api.myNotes(1, 1),
      this.api.myNotes(1, 1, 'ReturnedForRevision'),
      this.api.myAnnouncements(1, 3),
    ]);
    this.summary.set(summary);
    this.upcoming.set(upcoming);
    this.noteCount.set(notes.totalCount);
    this.returnedNoteCount.set(returned.totalCount);
    this.latestAnnouncements.set(announcements.items);
  }

  private async loadMentor(): Promise<void> {
    const [result, created, pendingJournal] = await Promise.all([
      this.api.myGroups(1, 6),
      this.api.createdTasks(1, 1),
      this.api.reviewQueue(1, 1, 'Submitted'),
    ]);
    this.groups.set(result.items);
    this.groupTotal.set(result.totalCount);
    this.createdTaskTotal.set(created.totalCount);
    this.pendingJournalCount.set(pendingJournal.totalCount);
  }

  private async loadAdmin(): Promise<void> {
    // Sadece toplam sayı lazım: 1 kayıtlık sayfa istemek yeterli (totalCount gelir).
    const [users, pending, groups, tasks] = await Promise.all([
      this.api.adminUsers(1, 1),
      this.api.adminPendingUsers(1, 5),
      this.api.adminGroups(1, 1),
      this.api.adminTasks(1, 1),
    ]);
    this.stats.set({
      users: users.totalCount,
      pending: pending.totalCount,
      groups: groups.totalCount,
      tasks: tasks.totalCount,
    });
    this.pendingUsers.set(pending.items);
  }

  protected async approve(user: User): Promise<void> {
    this.approvingId.set(user.id);
    try {
      await this.api.approveUser(user.id);
      this.toast.success(`${user.fullName} onaylandı.`);
      await this.loadAdmin();
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.approvingId.set(null);
    }
  }
}
