import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../shared/icon';

/** Tanımsız bir adres açılınca gösterilen 404 sayfası. */
@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="nf">
      <div class="code">404</div>
      <h1>Sayfa bulunamadı</h1>
      <p class="muted">Aradığın sayfa taşınmış ya da hiç var olmamış olabilir.</p>
      <a routerLink="/" class="btn btn-primary"><app-icon name="home" [size]="18" /> Ana sayfaya dön</a>
    </main>
  `,
  styles: `
    .nf {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 0.75rem;
      min-height: 100dvh;
      padding: 2rem 1rem;
      text-align: center;
    }
    .code {
      font-size: clamp(4rem, 14vw, 7rem);
      font-weight: 800;
      line-height: 1;
      letter-spacing: -0.04em;
      background: linear-gradient(135deg, var(--primary), #8b5cf6);
      -webkit-background-clip: text;
      background-clip: text;
      color: transparent;
    }
    .btn {
      margin-top: 0.75rem;
    }
  `,
})
export class NotFoundPage {}
