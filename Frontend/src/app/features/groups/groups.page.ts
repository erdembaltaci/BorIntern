import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { PAGE_SIZE } from '../../core/config';
import { ConfirmService } from '../../core/confirm.service';
import { formatDateTime } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { Group } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { Modal } from '../../shared/modal';
import { Pager } from '../../shared/pager';
import { SearchBox } from '../../shared/search-box';

/** Mentorun kendi gruplarını listelediği, oluşturduğu, yeniden adlandırdığı ve sildiği sayfa. */
@Component({
  selector: 'app-groups-page',
  imports: [ReactiveFormsModule, RouterLink, Icon, Modal, Pager, EmptyState, SearchBox],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './groups.page.html',
  styleUrl: './groups.page.css',
})
export class GroupsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly fb = inject(FormBuilder);

  /** Arama kutusundaki metin (boş = filtre yok). Değişince liste 1. sayfadan yeniden yüklenir. */
  protected readonly search = signal('');

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly groups = signal<Group[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);

  /** Diyalog: null = kapalı, 'new' = yeni grup, Group = o grubu yeniden adlandır. */
  protected readonly dialog = signal<'new' | Group | null>(null);
  protected readonly saving = signal(false);
  protected readonly nameControl = this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2)]);

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
      const result = await this.api.myGroups(page, PAGE_SIZE, this.search());
      if (result.items.length === 0 && page > 1) {
        await this.load(page - 1);
        return;
      }
      this.groups.set(result.items);
      this.page.set(result.page);
      this.totalPages.set(result.totalPages);
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  protected openCreate(): void {
    this.nameControl.reset('');
    this.dialog.set('new');
  }

  protected openRename(group: Group): void {
    this.nameControl.reset(group.name);
    this.dialog.set(group);
  }

  protected closeDialog(): void {
    this.dialog.set(null);
  }

  protected async save(): Promise<void> {
    const target = this.dialog();
    if (this.nameControl.invalid || target === null) {
      this.nameControl.markAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      const name = this.nameControl.value.trim();
      if (target === 'new') {
        await this.api.createGroup(name);
        this.toast.success('Grup oluşturuldu.');
        await this.load(1);
      } else {
        await this.api.updateGroup(target.id, name);
        this.toast.success('Grup adı güncellendi.');
        await this.load(this.page());
      }
      this.dialog.set(null);
    } catch (err) {
      // Örn. 409: "Bu grup adı zaten kullanılıyor." Diyalog açık kalır, kullanıcı adı düzeltebilir.
      this.toast.error(errorMessage(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(group: Group): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Grubu sil',
      message: `"${group.name}" grubu silinsin mi? Hemen ardından "Geri al" ile vazgeçebilirsin.`,
      confirmLabel: 'Sil',
      danger: true,
    });
    if (!ok) return;

    try {
      await this.api.deleteGroup(group.id);
      this.toast.undoable(`"${group.name}" silindi.`, () => void this.restore(group.id));
      await this.load(this.page());
    } catch (err) {
      this.toast.error(errorMessage(err));
    }
  }

  private async restore(groupId: number): Promise<void> {
    try {
      await this.api.restoreGroup(groupId);
      this.toast.success('Grup geri getirildi.');
      await this.load(this.page());
    } catch (err) {
      this.toast.error(errorMessage(err));
    }
  }
}
