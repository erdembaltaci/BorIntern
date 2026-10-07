import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, OnInit, effect, inject, input, signal, untracked } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { PAGE_SIZE } from '../../core/config';
import { formatDay, isPastDay, relativeDay } from '../../core/date.util';
import { errorMessage } from '../../core/error.util';
import { TaskItem, TaskStatus, TaskSummary } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { Badge } from '../../shared/badge';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { TASK_STATUSES, TASK_STATUS_LABEL } from '../../shared/labels';
import { Pager } from '../../shared/pager';

type Filter = 'All' | TaskStatus;
type View = 'board' | 'list';

/** Pano sütunu: o durumdaki görevlerden şimdiye kadar yüklenen kısım ve toplam bilgisi. */
interface Column {
  items: TaskItem[];
  total: number;
  page: number;
  totalPages: number;
  loading: boolean;
}

const VIEW_KEY = 'tasks.view';
const emptyColumn = (): Column => ({ items: [], total: 0, page: 0, totalPages: 1, loading: false });
const emptySummary = (): TaskSummary => ({ totalTasks: 0, todoCount: 0, inProgressCount: 0, completedCount: 0, overdueCount: 0 });

function readView(): View {
  try {
    return localStorage.getItem(VIEW_KEY) === 'list' ? 'list' : 'board';
  } catch {
    return 'board';
  }
}

/**
 * Stajyerin görevleri. Hiçbir görünümde "tüm görevler" bir seferde çekilmez; her şey sunucudan sayfa sayfa gelir:
 *  - Pano: üç sütun, her sütun kendi durum filtresiyle 10'ar görev yükler, "Daha fazla göster" sonraki sayfayı ister.
 *    Kartı başka sütuna sürükleyip bırakınca durum değişir.
 *  - Liste: filtre çipleri (durum, sunucuda süzülür) + sayfalama. Durum düğmeleriyle de değiştirilebilir (klavye/ekran okuyucu için).
 * Sütun başlığındaki sayılar ve çip sayıları görevlerden değil, `/tasks/mine/summary` özetinden gelir.
 */
@Component({
  selector: 'app-my-tasks-page',
  imports: [Icon, Badge, EmptyState, Pager, CdkDropListGroup, CdkDropList, CdkDrag],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './my-tasks.page.html',
  styleUrl: './my-tasks.page.css',
})
export class MyTasksPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  /** Panelden gelen kartlar /gorevler?durum=Todo gibi açar (withComponentInputBinding sayesinde sorgu parametresi input olur). */
  readonly durum = input<string>();

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly summary = signal<TaskSummary>(emptySummary());
  protected readonly view = signal<View>(readView());

  // Pano
  protected readonly columns = signal<Record<TaskStatus, Column>>({
    Todo: emptyColumn(),
    InProgress: emptyColumn(),
    Completed: emptyColumn(),
  });

  // Liste
  protected readonly filter = signal<Filter>('All');
  protected readonly listItems = signal<TaskItem[]>([]);
  protected readonly listPage = signal(1);
  protected readonly listTotalPages = signal(1);
  protected readonly listLoading = signal(false);
  /** Durumu şu an kaydedilmekte olan görev (liste görünümündeki butonları geçici kilitlemek için). */
  protected readonly savingId = signal<number | null>(null);

  protected readonly statuses = TASK_STATUSES;
  protected readonly statusLabel = TASK_STATUS_LABEL;
  protected readonly formatDay = formatDay;
  protected readonly relativeDay = relativeDay;
  protected readonly isPastDay = isPastDay;

  /** Son uygulanan adres durumu: effect ilk çalışmada (değişiklik yokken) boşuna ikinci kez yüklemesin diye. */
  private appliedDurum: string | undefined;
  private initialized = false;

  constructor() {
    // Aynı sayfadayken adres değişirse (ör. panelden başka bir karta basılırsa) yeni duruma göre yeniden yükle.
    effect(() => {
      const wanted = this.durum();
      untracked(() => {
        if (!this.initialized || wanted === this.appliedDurum) return;
        this.applyDurum(wanted);
        void this.load();
      });
    });
  }

  ngOnInit(): void {
    // Girdiler ngOnInit'ten önce atanır; ilk yüklemede adresteki durum doğrudan okunur.
    this.applyDurum(this.durum());
    this.initialized = true;
    void this.load();
  }

  /** Adreste geçerli bir durum varsa (panel kartlarından gelindiğinde) o duruma filtreli LİSTE görünümü açılır. */
  private applyDurum(wanted: string | undefined): void {
    this.appliedDurum = wanted;
    const status = TASK_STATUSES.find((s) => s === wanted);
    this.filter.set(status ?? 'All');
    if (status) this.view.set('list');
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      // Özet + açık olan görünümün ilk sayfaları paralel gelir.
      await Promise.all([
        this.refreshSummary(),
        this.view() === 'board' ? this.loadAllColumns() : this.loadList(1),
      ]);
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  private async refreshSummary(): Promise<void> {
    this.summary.set(await this.api.mySummary());
  }

  protected setView(view: View): void {
    if (this.view() === view) return;
    this.view.set(view);
    try {
      localStorage.setItem(VIEW_KEY, view);
    } catch {
      /* tercih sadece bu oturumda geçerli olur */
    }
    void this.load();
  }

  // ---------------------------------------------------------------- Pano

  private loadAllColumns(): Promise<unknown> {
    return Promise.all(this.statuses.map((status) => this.loadColumn(status, 1)));
  }

  private async loadColumn(status: TaskStatus, page: number): Promise<void> {
    this.patchColumn(status, { loading: true });
    try {
      const result = await this.api.myTasks(page, PAGE_SIZE, status);
      const current = this.columns()[status].items;
      // Ekleme (sonraki sayfa) modunda, sürükleyerek eklenmiş kartlarla çakışmasın diye Id'ye göre tekilleştirilir.
      const merged = page === 1 ? result.items : [...current, ...result.items.filter((t) => !current.some((c) => c.id === t.id))];
      this.patchColumn(status, { items: merged, total: result.totalCount, page: result.page, totalPages: result.totalPages, loading: false });
    } catch (err) {
      this.patchColumn(status, { loading: false });
      throw err;
    }
  }

  protected async loadMore(status: TaskStatus): Promise<void> {
    const column = this.columns()[status];
    if (column.loading || column.page >= column.totalPages) return;
    try {
      await this.loadColumn(status, column.page + 1);
    } catch (err) {
      this.toast.error(errorMessage(err));
    }
  }

  private patchColumn(status: TaskStatus, patch: Partial<Column>): void {
    this.columns.update((c) => ({ ...c, [status]: { ...c[status], ...patch } }));
  }

  /** Kartı bir sütundan diğerine yerel olarak taşır (iyimser güncelleme ve geri alma için ortak). */
  private moveLocally(task: TaskItem, from: TaskStatus, to: TaskStatus): void {
    this.columns.update((c) => ({
      ...c,
      [from]: { ...c[from], items: c[from].items.filter((t) => t.id !== task.id), total: Math.max(0, c[from].total - 1) },
      [to]: { ...c[to], items: [{ ...task, status: to }, ...c[to].items.filter((t) => t.id !== task.id)], total: c[to].total + 1 },
    }));
  }

  /**
   * Pano görünümü: kart başka bir sütuna bırakıldı. Kart anında yeni sütunda görünür (iyimser güncelleme);
   * sunucu reddederse eski sütununa geri döner ve hata gösterilir.
   */
  protected async drop(event: CdkDragDrop<TaskItem[]>, target: TaskStatus): Promise<void> {
    // Aynı sütun içinde bırakma durum değiştirmez, yapılacak bir şey yok.
    if (event.previousContainer === event.container) return;

    const task = event.item.data as TaskItem;
    const from = task.status;
    this.moveLocally(task, from, target);

    try {
      const updated = await this.api.updateTaskStatus(task.id, target);
      this.columns.update((c) => ({ ...c, [target]: { ...c[target], items: c[target].items.map((t) => (t.id === updated.id ? updated : t)) } }));
      // Geciken sayısı gibi özet değerleri değişmiş olabilir: sayılar sunucudan tazelenir.
      await this.refreshSummary();
      if (target === 'Completed') this.toast.success(`"${task.title}" tamamlandı. Tebrikler! 🎉`);
    } catch (err) {
      this.moveLocally({ ...task, status: target }, target, from);
      this.toast.error(errorMessage(err));
    }
  }

  // ---------------------------------------------------------------- Liste

  protected async loadList(page: number): Promise<void> {
    this.listLoading.set(true);
    try {
      const filter = this.filter();
      const result = await this.api.myTasks(page, PAGE_SIZE, filter === 'All' ? undefined : filter);
      this.listItems.set(result.items);
      this.listPage.set(result.page);
      this.listTotalPages.set(result.totalPages);
    } finally {
      this.listLoading.set(false);
    }
  }

  protected async setFilter(filter: Filter): Promise<void> {
    this.filter.set(filter);
    try {
      await this.loadList(1);
    } catch (err) {
      this.toast.error(errorMessage(err));
    }
  }

  protected async goToListPage(page: number): Promise<void> {
    try {
      await this.loadList(page);
    } catch (err) {
      this.toast.error(errorMessage(err));
    }
  }

  /** Liste görünümü: alttaki durum düğmeleri. Filtre aktifse görev listeden çıkabileceği için sayfa ve sayılar sunucudan tazelenir. */
  protected async changeStatus(task: TaskItem, status: TaskStatus): Promise<void> {
    if (task.status === status || this.savingId() !== null) return;

    this.savingId.set(task.id);
    try {
      await this.api.updateTaskStatus(task.id, status);
      const page = this.listPage();
      await Promise.all([this.refreshSummary(), this.loadList(page)]);
      // Son sayfadaki tek görev başka duruma geçtiyse o sayfa boşalır: bir önceki sayfaya düş.
      if (this.listItems().length === 0 && page > 1) await this.loadList(page - 1);
      if (status === 'Completed') this.toast.success(`"${task.title}" tamamlandı. Tebrikler! 🎉`);
    } catch (err) {
      this.toast.error(errorMessage(err));
    } finally {
      this.savingId.set(null);
    }
  }

  protected countFor(filter: Filter): number {
    const s = this.summary();
    switch (filter) {
      case 'Todo':
        return s.todoCount;
      case 'InProgress':
        return s.inProgressCount;
      case 'Completed':
        return s.completedCount;
      default:
        return s.totalTasks;
    }
  }
}
