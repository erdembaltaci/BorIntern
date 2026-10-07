import { Injectable, signal } from '@angular/core';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  /** true ise onay butonu kırmızı olur (geri alınamaz/riskli işlemler). */
  danger?: boolean;
}

interface PendingConfirm extends ConfirmOptions {
  resolve: (result: boolean) => void;
}

/**
 * Tarayıcının çirkin `confirm()` penceresi yerine kendi diyaloğumuz.
 * Kullanım:  if (!(await this.confirm.ask({ ... }))) return;
 */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  readonly pending = signal<PendingConfirm | null>(null);

  ask(options: ConfirmOptions): Promise<boolean> {
    return new Promise<boolean>((resolve) => {
      this.pending.set({ ...options, resolve });
    });
  }

  answer(result: boolean): void {
    this.pending()?.resolve(result);
    this.pending.set(null);
  }
}
