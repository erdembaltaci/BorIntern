import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { PAGE_SIZE } from '../../core/config';
import { ConfirmService } from '../../core/confirm.service';
import { formatDateTime } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { PasswordResetLink, Role, User } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { Badge } from '../../shared/badge';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { ROLES, ROLE_LABEL, initials } from '../../shared/labels';
import { Modal } from '../../shared/modal';
import { Pager } from '../../shared/pager';
import { SearchBox } from '../../shared/search-box';

type Tab = 'all' | 'pending';

/** Yönetici: kullanıcıları listeler, onaylar/pasifleştirir ve rol atar. */
@Component({
  selector: 'app-admin-users-page',
  imports: [Icon, Badge, Modal, Pager, EmptyState, SearchBox],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-users.page.html',
  styleUrl: './admin-users.page.css',
})
export class AdminUsersPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  protected readonly currentUserId = inject(AuthService).user()?.id;

  protected readonly tab = signal<Tab>('all');
  /** Arama kutusundaki metin (boş = filtre yok). Değişince liste 1. sayfadan yeniden yüklenir. */
  protected readonly search = signal('');

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly users = signal<User[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly totalCount = signal(0);
  protected readonly pendingCount = signal(0);
  /** Şu an işlem yapılan kullanıcı (o satırın butonlarını kilitlemek için). */
  protected readonly actingId = signal<number | null>(null);

  // Parola sıfırlama bağlantısı diyaloğu: null = kapalı. Ham anahtar yalnızca burada, bir kez görünür.
  protected readonly resetLink = signal<PasswordResetLink | null>(null);
  protected readonly copied = signal(false);

  protected readonly roles = ROLES;
  protected readonly roleLabel = ROLE_LABEL;
  protected readonly initials = initials;
  protected readonly formatDateTime = formatDateTime;

  ngOnInit(): void {
    void this.load(1);
  }

  protected setTab(tab: Tab): void {
    if (this.tab() === tab) return;
    this.tab.set(tab);
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
      const loader =
        this.tab() === 'all'
          ? this.api.adminUsers(page, PAGE_SIZE, this.search())
          : this.api.adminPendingUsers(page, PAGE_SIZE, this.search());
      // Sekmedeki "Onay bekleyen" sayısı her zaman güncel kalsın diye ayrıca 1 kayıtlık istek atılır.
      const [result, pending] = await Promise.all([loader, this.api.adminPendingUsers(1, 1)]);

      if (result.items.length === 0 && page > 1) {
        await this.load(page - 1);
        return;
      }
      this.users.set(result.items);
      this.page.set(result.page);
      this.totalPages.set(result.totalPages);
      this.totalCount.set(result.totalCount);
      this.pendingCount.set(pending.totalCount);
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * E-posta servisi olmadığında parola sıfırlama: yönetici bağlantıyı üretir ve kullanıcıya güvenli bir kanaldan iletir.
   * Üretmek parolayı ya da açık oturumları değiştirmez; yalnızca önceki bağlantıları geçersiz kılar.
   */
  protected async createResetLink(user: User): Promise<void> {
    this.actingId.set(user.id);
    try {
      this.copied.set(false);
      this.resetLink.set(await this.api.createResetLink(user.id));
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.actingId.set(null);
    }
  }

  protected closeResetLink(): void {
    this.resetLink.set(null);
  }

  /** Panoya kopyalar; tarayıcı izin vermezse kullanıcı kutudaki metni elle kopyalayabilsin diye seçili bırakılır. */
  protected async copyResetLink(input: HTMLInputElement): Promise<void> {
    input.select();
    try {
      await navigator.clipboard.writeText(input.value);
      this.copied.set(true);
    } catch {
      this.toast.info('Otomatik kopyalanamadı; seçili bağlantıyı Ctrl+C ile kopyala.');
    }
  }

  /** Pending ya da Inactive kullanıcıyı Active yapar. */
  protected async approve(user: User): Promise<void> {
    await this.run(user, () => this.api.approveUser(user.id), `${user.fullName} aktifleştirildi.`);
  }

  protected async deactivate(user: User): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Kullanıcıyı pasifleştir',
      message: `${user.fullName} pasifleştirilsin mi? Giriş yapamaz ve mevcut oturumu hemen geçersiz olur. İstersen daha sonra tekrar aktifleştirebilirsin.`,
      confirmLabel: 'Pasifleştir',
      danger: true,
    });
    if (!ok) return;
    await this.run(user, () => this.api.deactivateUser(user.id), `${user.fullName} pasifleştirildi.`);
  }

  protected async changeRole(user: User, select: HTMLSelectElement): Promise<void> {
    const role = select.value as Role;
    if (role === user.role) return;

    const ok = await this.confirm.ask({
      title: 'Rolü değiştir',
      message: `${user.fullName} kullanıcısının rolü "${ROLE_LABEL[user.role]}" yerine "${ROLE_LABEL[role]}" olarak değiştirilsin mi? Değişiklik hemen geçerli olur.`,
      confirmLabel: 'Rolü değiştir',
    });
    if (!ok) {
      // Kullanıcı vazgeçti: <select> ekranda yeni seçeneği gösteriyor, eski haline döndür.
      select.value = user.role;
      return;
    }
    const done = await this.run(user, () => this.api.changeUserRole(user.id, role), `${user.fullName} artık ${ROLE_LABEL[role]}.`);
    if (!done) select.value = user.role;
  }

  /** Bir işlemi çalıştırır, satırı kilitler, sonucu bildirir ve listeyi tazeler. Başarılıysa true döner. */
  private async run(user: User, action: () => Promise<unknown>, successMessage: string): Promise<boolean> {
    this.actingId.set(user.id);
    try {
      await action();
      this.toast.success(successMessage);
      await this.load(this.page());
      return true;
    } catch (err) {
      this.toast.error(errorMessage(err));
      return false;
    } finally {
      this.actingId.set(null);
    }
  }
}
