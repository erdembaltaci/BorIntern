import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  type: 'success' | 'error' | 'info';
  message: string;
  /** Varsa bildirimin üstünde bir buton çıkar (ör. "Geri al"). */
  actionLabel?: string;
  action?: () => void;
}

/** Ekranın köşesinde kısa süre görünen bildirimler. Gösterim ToastContainer bileşeninde. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<Toast[]>([]);
  private nextId = 1;

  success(message: string): void {
    this.push({ type: 'success', message }, 4000);
  }

  error(message: string): void {
    this.push({ type: 'error', message }, 7000);
  }

  info(message: string): void {
    this.push({ type: 'info', message }, 5000);
  }

  /**
   * "Geri al" butonlu bildirim. Silme işlemleri soft delete olduğu için onay sormak yerine
   * işlemi hemen yapıp kısa bir süre geri alma imkânı veriyoruz (restore uç noktası bunun için var).
   */
  undoable(message: string, action: () => void, actionLabel = 'Geri al'): void {
    this.push({ type: 'success', message, actionLabel, action }, 9000);
  }

  dismiss(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private push(toast: Omit<Toast, 'id'>, durationMs: number): void {
    const id = this.nextId++;
    this.toasts.update((list) => [...list, { ...toast, id }]);
    setTimeout(() => this.dismiss(id), durationMs);
  }
}
