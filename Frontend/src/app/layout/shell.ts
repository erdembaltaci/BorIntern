import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { ThemeService } from '../core/theme.service';
import { Role } from '../core/models';
import { Icon } from '../shared/icon';
import { Badge } from '../shared/badge';
import { initials } from '../shared/labels';

interface NavItem {
  label: string;
  link: string;
  icon: string;
  roles: Role[] | 'all';
  /** true ise sadece tam eşleşmede aktif görünür (Panel '/' her adresin başı olduğu için gerekli). */
  exact?: boolean;
}

// Menü tek bir listeden üretilir; her rol sadece kendi sayfalarını görür.
const NAV_ITEMS: NavItem[] = [
  { label: 'Panel', link: '/', icon: 'home', roles: 'all', exact: true },
  { label: 'Görevlerim', link: '/gorevler', icon: 'tasks', roles: ['Intern'] },
  { label: 'Staj defterim', link: '/defter', icon: 'notes', roles: ['Intern'] },
  { label: 'Duyurular', link: '/duyurular', icon: 'megaphone', roles: ['Intern'] },
  { label: 'Gruplarım', link: '/gruplar', icon: 'folder', roles: ['Mentor'] },
  { label: 'Atadığım görevler', link: '/atadigim-gorevler', icon: 'tasks', roles: ['Mentor'] },
  { label: 'Defter onayları', link: '/defter-onaylari', icon: 'check-circle', roles: ['Mentor'] },
  { label: 'Kullanıcılar', link: '/yonetim/kullanicilar', icon: 'users', roles: ['Admin'] },
  { label: 'Tüm gruplar', link: '/yonetim/gruplar', icon: 'folder', roles: ['Admin'] },
  { label: 'Tüm görevler', link: '/yonetim/gorevler', icon: 'tasks', roles: ['Admin'] },
  { label: 'Profilim', link: '/profil', icon: 'user', roles: 'all' },
];

/** Giriş yapmış kullanıcıların ortak düzeni: yan menü (mobilde açılır çekmece) + sayfa içeriği. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon, Badge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  private readonly auth = inject(AuthService);
  protected readonly themeService = inject(ThemeService);

  protected readonly user = this.auth.user;
  protected readonly userInitials = computed(() => initials(this.user()?.fullName ?? ''));

  protected readonly navItems = computed(() => {
    const role = this.auth.role();
    return NAV_ITEMS.filter((item) => item.roles === 'all' || (role !== null && item.roles.includes(role)));
  });

  /** Mobilde menü çekmecesi açık mı? Masaüstünde menü her zaman görünür, bu değer önemsizdir. */
  protected readonly menuOpen = signal(false);

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
  }

  protected logout(): void {
    this.closeMenu();
    void this.auth.logout();
  }
}
