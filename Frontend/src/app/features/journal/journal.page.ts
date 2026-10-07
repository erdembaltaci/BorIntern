import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { PAGE_SIZE } from '../../core/config';
import { ConfirmService } from '../../core/confirm.service';
import { formatDateTime, formatDayLong, formatHours, fromInputDay, toInputDay, todayInputValue } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { Note, NoteStatus } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { Badge } from '../../shared/badge';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { NOTE_STATUSES, NOTE_STATUS_LABEL } from '../../shared/labels';
import { Modal } from '../../shared/modal';
import { Pager } from '../../shared/pager';
import { SearchBox } from '../../shared/search-box';

type Filter = 'All' | NoteStatus;

/**
 * Stajyerin staj defteri: günde tek kayıt (başlık, yapılan iş, öğrenilenler, süre, etiket).
 * Kayıt önce TASLAK'tır; "Mentora gönder" ile mentorun onayına gider. Mentor onaylar ya da açıklamayla düzeltme ister.
 * Sadece taslak ve "düzeltme istendi" kayıtlar düzenlenip silinebilir; gönderilen kayıt geri çekilebilir, onaylı kayıt kilitlidir.
 */
@Component({
  selector: 'app-journal-page',
  imports: [ReactiveFormsModule, RouterLink, Icon, Badge, Modal, Pager, EmptyState, SearchBox],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './journal.page.html',
  styleUrl: './journal.page.css',
})
export class JournalPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly fb = inject(FormBuilder);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<Note[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly totalCount = signal(0);

  protected readonly search = signal('');
  protected readonly filter = signal<Filter>('All');
  /** Uzun metinleri açıp kapatmak için: açık kartların Id'leri. */
  protected readonly expanded = signal<ReadonlySet<number>>(new Set());
  /** Üzerinde işlem yapılan kayıt (butonları geçici kilitlemek için). */
  protected readonly busyId = signal<number | null>(null);

  // Kayıt formu (diyalog): null = kapalı, 'new' = yeni kayıt, Note = o kaydı düzenle
  protected readonly dialog = signal<'new' | Note | null>(null);
  protected readonly saving = signal(false);
  protected readonly form = this.fb.group({
    noteDate: this.fb.nonNullable.control(todayInputValue(), Validators.required),
    title: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(150)]),
    content: this.fb.nonNullable.control('', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(4000)]),
    learned: this.fb.nonNullable.control('', Validators.maxLength(2000)),
    hoursSpent: this.fb.control<number | null>(null, [Validators.min(0.25), Validators.max(24)]),
    tags: this.fb.nonNullable.control('', Validators.maxLength(200)),
  });

  protected readonly statuses = NOTE_STATUSES;
  protected readonly statusLabel = NOTE_STATUS_LABEL;
  protected readonly todayValue = todayInputValue();
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
      const result = await this.api.myNotes(page, PAGE_SIZE, filter === 'All' ? undefined : filter, this.search());
      // Son sayfadaki tek kayıt silinince o sayfa boşalır; bir önceki sayfaya düş.
      if (result.items.length === 0 && page > 1) {
        await this.load(page - 1);
        return;
      }
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

  protected onSearch(term: string): void {
    this.search.set(term);
    void this.load(1);
  }

  protected setFilter(filter: Filter): void {
    this.filter.set(filter);
    void this.load(1);
  }

  protected canEdit(note: Note): boolean {
    return note.status === 'Draft' || note.status === 'ReturnedForRevision';
  }

  protected tagList(note: Note): string[] {
    return note.tags ? note.tags.split(',').map((t) => t.trim()).filter(Boolean) : [];
  }

  protected isLong(note: Note): boolean {
    return note.content.length > 280 || note.learned.length > 200;
  }

  protected toggle(id: number): void {
    this.expanded.update((set) => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  // ------------------------------------------------------------ form

  protected openNew(): void {
    this.form.reset({ noteDate: todayInputValue(), title: '', content: '', learned: '', hoursSpent: null, tags: '' });
    this.dialog.set('new');
  }

  protected openEdit(note: Note): void {
    this.form.reset({
      noteDate: toInputDay(note.noteDate) || todayInputValue(),
      title: note.title,
      content: note.content,
      learned: note.learned,
      hoursSpent: note.hoursSpent,
      tags: note.tags,
    });
    this.dialog.set(note);
  }

  protected closeDialog(): void {
    this.dialog.set(null);
  }

  protected async save(): Promise<void> {
    const target = this.dialog();
    if (this.form.invalid || target === null) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      const value = this.form.getRawValue();
      const payload = {
        noteDate: fromInputDay(value.noteDate),
        title: value.title.trim(),
        content: value.content.trim(),
        learned: value.learned.trim(),
        hoursSpent: value.hoursSpent,
        tags: value.tags.trim(),
      };

      if (target === 'new') {
        await this.api.createNote(payload);
        this.toast.success('Defter kaydı taslak olarak kaydedildi.');
        await this.load(1);
      } else {
        await this.api.updateNote(target.id, payload);
        this.toast.success('Kayıt güncellendi.');
        await this.load(this.page());
      }
      this.dialog.set(null);
    } catch (err) {
      // Örn. 409 "Bu güne ait bir defter kaydın zaten var": diyalog açık kalır, kullanıcı tarihi düzeltebilir.
      this.toast.error(errorMessage(err));
    } finally {
      this.saving.set(false);
    }
  }

  // ------------------------------------------------------------ durum akışı

  protected async submit(note: Note): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Mentora gönder',
      message: `"${note.title || 'Başlıksız kayıt'}" mentorunun onayına gönderilsin mi? Mentor değerlendirene kadar düzenleyemezsin (istersen geri çekebilirsin).`,
      confirmLabel: 'Gönder',
    });
    if (!ok) return;
    await this.run(note, () => this.api.submitNote(note.id), 'Kayıt mentora gönderildi.');
  }

  protected async withdraw(note: Note): Promise<void> {
    await this.run(note, () => this.api.withdrawNote(note.id), 'Kayıt geri çekildi, tekrar düzenleyebilirsin.');
  }

  /** Önce onay sorulur; silme soft delete olduğu için yine de kısa süre "Geri al" fırsatı verilir. */
  protected async remove(note: Note): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Kaydı sil',
      message: `${formatDayLong(note.noteDate)} tarihli kayıt silinsin mi? Hemen ardından "Geri al" ile vazgeçebilirsin.`,
      confirmLabel: 'Sil',
      danger: true,
    });
    if (!ok) return;

    this.busyId.set(note.id);
    try {
      await this.api.deleteNote(note.id);
      this.toast.undoable('Kayıt silindi.', () => void this.restore(note.id));
      await this.load(this.page());
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.busyId.set(null);
    }
  }

  private async restore(noteId: number): Promise<void> {
    try {
      await this.api.restoreNote(noteId);
      this.toast.success('Kayıt geri getirildi.');
      await this.load(this.page());
    } catch (err) {
      this.toast.error(errorMessage(err));
    }
  }

  private async run(note: Note, action: () => Promise<unknown>, successMessage: string): Promise<void> {
    this.busyId.set(note.id);
    try {
      await action();
      this.toast.success(successMessage);
      await this.load(this.page());
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.busyId.set(null);
    }
  }
}
