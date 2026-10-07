import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { debounceTime, distinctUntilChanged, from, startWith, switchMap } from 'rxjs';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { PAGE_SIZE } from '../../core/config';
import { ConfirmService } from '../../core/confirm.service';
import { formatDateTime, fromInputDay } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { Announcement, Group, GroupMember, TaskSummary, User } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { initials } from '../../shared/labels';
import { Modal } from '../../shared/modal';
import { Pager } from '../../shared/pager';

/**
 * Tek bir grubun detayı: üyeleri listeler, üye ekler/çıkarır, üyeye görev atar ve görev ilerlemesini gösterir.
 *
 * Stajyer eklemek için ad/e-posta ile arama yapılır (GET /api/users/interns); sadece aktif stajyerler listelenir.
 */
@Component({
  selector: 'app-group-detail-page',
  imports: [ReactiveFormsModule, RouterLink, Icon, Modal, Pager, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './group-detail.page.html',
  styleUrl: './group-detail.page.css',
})
export class GroupDetailPage {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly fb = inject(FormBuilder);

  /** Rota parametresi (/gruplar/:id). withComponentInputBinding sayesinde otomatik gelir ve string'dir. */
  readonly id = input.required<string>();

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly group = signal<Group | null>(null);

  protected readonly members = signal<GroupMember[]>([]);
  protected readonly membersLoading = signal(false);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly totalCount = signal(0);
  /** Her üyenin (userId -> özet) görev sayıları. null = yüklenemedi. */
  protected readonly summaries = signal<Record<number, TaskSummary | null>>({});

  // Üye ekleme: arama kutusu + sonuç listesi
  protected readonly searchControl = this.fb.nonNullable.control('');
  protected readonly searchResults = signal<User[]>([]);
  protected readonly searching = signal(false);
  protected readonly searchOpen = signal(false);
  protected readonly addingUserId = signal<number | null>(null);
  private readonly destroyRef = inject(DestroyRef);

  // Görev atama diyaloğu: atanacak üye (null = kapalı)
  protected readonly taskTarget = signal<GroupMember | null>(null);
  protected readonly savingTask = signal(false);
  protected readonly taskForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.minLength(2)]],
    description: [''],
    dueDate: [''],
  });

  // Duyurular: mentor kendi grubuna yazar, grubun üyeleri okur
  protected readonly announcements = signal<Announcement[]>([]);
  protected readonly annPage = signal(1);
  protected readonly annTotalPages = signal(1);
  protected readonly annLoading = signal(false);
  protected readonly postingAnnouncement = signal(false);
  protected readonly announcementForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(150)]],
    content: ['', [Validators.required, Validators.maxLength(2000), Validators.pattern(/\S/)]],
  });

  protected readonly groupId = computed(() => Number(this.id()));
  /** Arama sonuçlarından, bu sayfadaki üyeleri çıkar (başka sayfadakiler için backend zaten 409 döner). */
  protected readonly candidates = computed(() => {
    const memberIds = new Set(this.members().map((m) => m.userId));
    return this.searchResults().filter((u) => !memberIds.has(u.id));
  });
  protected readonly formatDateTime = formatDateTime;
  protected readonly initials = initials;

  constructor() {
    // Yazdıkça 300 ms bekleyip arar (her tuşta istek atmamak için). switchMap, eski aramanın geç gelen cevabını yok sayar.
    this.searchControl.valueChanges
      .pipe(
        startWith(''),
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => {
          this.searching.set(true);
          return from(
            this.api
              .searchInterns(term.trim(), 1, 8)
              .then((result) => result.items)
              .catch((err) => {
                this.toast.error(errorMessage(err));
                return [] as User[];
              }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((users) => {
        this.searchResults.set(users);
        this.searching.set(false);
      });

    // Adres çubuğunda grup numarası değişirse (aynı bileşen yeniden kullanılır) verileri yeniden yükle.
    effect(() => {
      this.groupId();
      untracked(() => void this.loadAll());
    });
  }

  private async loadAll(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.group.set(null);
    try {
      if (!Number.isInteger(this.groupId()) || this.groupId() < 1) {
        throw new Error('invalid-id');
      }
      this.group.set(await this.api.group(this.groupId()));
      await this.loadMembers(1);
      void this.loadAnnouncements(1);
    } catch (err) {
      this.error.set(err instanceof Error && err.message === 'invalid-id' ? 'Geçersiz grup adresi.' : errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  protected async loadMembers(page: number): Promise<void> {
    this.membersLoading.set(true);
    try {
      const result = await this.api.groupMembers(this.groupId(), page, PAGE_SIZE);
      if (result.items.length === 0 && page > 1) {
        await this.loadMembers(page - 1);
        return;
      }
      this.members.set(result.items);
      this.page.set(result.page);
      this.totalPages.set(result.totalPages);
      this.totalCount.set(result.totalCount);
      void this.loadSummaries(result.items);
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.membersLoading.set(false);
    }
  }

  /** Üyelerin görev özetlerini paralel çeker; biri başarısız olursa diğerleri etkilenmez. */
  private async loadSummaries(members: GroupMember[]): Promise<void> {
    const results = await Promise.allSettled(members.map((m) => this.api.taskSummary(m.userId)));
    const next: Record<number, TaskSummary | null> = {};
    members.forEach((m, index) => {
      const result = results[index];
      next[m.userId] = result.status === 'fulfilled' ? result.value : null;
    });
    this.summaries.update((current) => ({ ...current, ...next }));
  }

  protected percent(summary: TaskSummary | null | undefined): number {
    return summary && summary.totalTasks > 0 ? Math.round((summary.completedCount / summary.totalTasks) * 100) : 0;
  }

  protected async addIntern(user: User): Promise<void> {
    this.addingUserId.set(user.id);
    try {
      await this.api.addGroupMember(this.groupId(), user.id);
      this.toast.success(`${user.fullName} gruba eklendi.`);
      this.searchControl.setValue('');
      this.searchOpen.set(false);
      await this.loadMembers(this.page());
    } catch (err) {
      // Örn. 409 "zaten üye".
      this.toast.error(errorMessage(err));
    } finally {
      this.addingUserId.set(null);
    }
  }

  protected async loadAnnouncements(page: number): Promise<void> {
    this.annLoading.set(true);
    try {
      const result = await this.api.groupAnnouncements(this.groupId(), page, 5);
      if (result.items.length === 0 && page > 1) {
        await this.loadAnnouncements(page - 1);
        return;
      }
      this.announcements.set(result.items);
      this.annPage.set(result.page);
      this.annTotalPages.set(result.totalPages);
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.annLoading.set(false);
    }
  }

  protected async sendAnnouncement(): Promise<void> {
    if (this.announcementForm.invalid) {
      this.announcementForm.markAllAsTouched();
      return;
    }

    this.postingAnnouncement.set(true);
    try {
      const { title, content } = this.announcementForm.getRawValue();
      await this.api.createAnnouncement(this.groupId(), title.trim(), content.trim());
      this.announcementForm.reset({ title: '', content: '' });
      this.toast.success('Duyuru gruba gönderildi.');
      await this.loadAnnouncements(1);
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.postingAnnouncement.set(false);
    }
  }

  protected async removeAnnouncement(item: Announcement): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Duyuruyu sil',
      message: `"${item.title}" duyurusu silinsin mi? Grubun üyeleri artık göremez.`,
      confirmLabel: 'Sil',
      danger: true,
    });
    if (!ok) return;

    try {
      await this.api.deleteAnnouncement(this.groupId(), item.id);
      this.toast.success('Duyuru silindi.');
      await this.loadAnnouncements(this.annPage());
    } catch (err) {
      this.toast.error(errorMessage(err));
    }
  }

  protected async removeMember(member: GroupMember): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Üyeyi çıkar',
      message: `${member.fullName || 'Stajyer #' + member.userId} bu gruptan çıkarılsın mı? Atanmış görevleri silinmez.`,
      confirmLabel: 'Çıkar',
      danger: true,
    });
    if (!ok) return;

    try {
      await this.api.removeGroupMember(this.groupId(), member.userId);
      this.toast.success('Üye gruptan çıkarıldı.');
      await this.loadMembers(this.page());
    } catch (err) {
      this.toast.error(errorMessage(err));
    }
  }

  protected openTaskDialog(member: GroupMember): void {
    this.taskForm.reset({ title: '', description: '', dueDate: '' });
    this.taskTarget.set(member);
  }

  protected closeTaskDialog(): void {
    this.taskTarget.set(null);
  }

  protected async assignTask(): Promise<void> {
    const member = this.taskTarget();
    if (this.taskForm.invalid || member === null) {
      this.taskForm.markAllAsTouched();
      return;
    }

    this.savingTask.set(true);
    try {
      const { title, description, dueDate } = this.taskForm.getRawValue();
      const task = await this.api.createTask({
        title: title.trim(),
        description: description.trim(),
        dueDate: fromInputDay(dueDate),
        assignedUserId: member.userId,
      });
      this.taskTarget.set(null);
      // Mentorun görev listeleme ekranı yok; yanlış atamayı düzeltebilsin diye kısa süreliğine geri alma sunuyoruz.
      this.toast.undoable(`Görev atandı: "${task.title}"`, () => void this.undoTask(task.id, member.userId));
      await this.loadSummaries([member]);
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.savingTask.set(false);
    }
  }

  private async undoTask(taskId: number, userId: number): Promise<void> {
    try {
      await this.api.deleteTask(taskId);
      this.toast.success('Görev geri alındı.');
      const member = this.members().find((m) => m.userId === userId);
      if (member) await this.loadSummaries([member]);
    } catch (err) {
      this.toast.error(errorMessage(err));
    }
  }
}
