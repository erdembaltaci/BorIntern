import { Injectable, signal } from '@angular/core';

type Theme = 'light' | 'dark';

/** Açık/koyu tema. Seçim localStorage'da tutulur; seçim yoksa işletim sisteminin teması kullanılır. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<Theme>(this.initialTheme());

  constructor() {
    this.apply(this.theme());
  }

  toggle(): void {
    const next: Theme = this.theme() === 'dark' ? 'light' : 'dark';
    this.theme.set(next);
    this.apply(next);
    try {
      localStorage.setItem('theme', next);
    } catch {
      /* localStorage kapalıysa tercih sadece bu oturumda geçerli olur */
    }
  }

  private apply(theme: Theme): void {
    document.documentElement.setAttribute('data-theme', theme);
  }

  private initialTheme(): Theme {
    try {
      const saved = localStorage.getItem('theme');
      if (saved === 'dark' || saved === 'light') return saved;
    } catch {
      /* yoksay */
    }
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
}
