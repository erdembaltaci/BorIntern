import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/guards';

// Sayfalar lazy-load edilir (loadComponent): kullanıcı bir sayfaya girince o sayfanın kodu iner, ilk açılış hızlı olur.
export const routes: Routes = [
  // Giriş yapmamış kullanıcıların sayfaları
  {
    path: 'giris',
    canActivate: [guestGuard],
    title: 'Giriş yap · Pusula',
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'kayit',
    canActivate: [guestGuard],
    title: 'Kayıt ol · Pusula',
    loadComponent: () => import('./features/auth/register.page').then((m) => m.RegisterPage),
  },

  // Giriş yapmış kullanıcıların sayfaları: hepsi yan menülü ortak düzenin (Shell) içinde açılır
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      {
        path: '',
        pathMatch: 'full',
        title: 'Panel · Pusula',
        loadComponent: () => import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'gorevler',
        canActivate: [roleGuard('Intern')],
        title: 'Görevlerim · Pusula',
        loadComponent: () => import('./features/tasks/my-tasks.page').then((m) => m.MyTasksPage),
      },
      {
        path: 'defter',
        canActivate: [roleGuard('Intern')],
        title: 'Staj defterim · Pusula',
        loadComponent: () => import('./features/journal/journal.page').then((m) => m.JournalPage),
      },
      {
        path: 'defter/yazdir',
        canActivate: [roleGuard('Intern')],
        title: 'Defteri yazdır · Pusula',
        loadComponent: () => import('./features/journal/journal-print.page').then((m) => m.JournalPrintPage),
      },
      {
        path: 'defter-onaylari',
        canActivate: [roleGuard('Mentor')],
        title: 'Defter onayları · Pusula',
        loadComponent: () => import('./features/journal/journal-review.page').then((m) => m.JournalReviewPage),
      },
      // Eski adres (önceki sürümde "Notlarım"): kayıtlı yer imleri çalışmaya devam etsin.
      { path: 'notlar', pathMatch: 'full', redirectTo: 'defter' },
      {
        path: 'duyurular',
        canActivate: [roleGuard('Intern')],
        title: 'Duyurular · Pusula',
        loadComponent: () => import('./features/announcements/announcements.page').then((m) => m.AnnouncementsPage),
      },
      {
        path: 'gruplar',
        canActivate: [roleGuard('Mentor')],
        title: 'Gruplarım · Pusula',
        loadComponent: () => import('./features/groups/groups.page').then((m) => m.GroupsPage),
      },
      {
        path: 'gruplar/:id',
        canActivate: [roleGuard('Mentor')],
        title: 'Grup detayı · Pusula',
        loadComponent: () => import('./features/groups/group-detail.page').then((m) => m.GroupDetailPage),
      },
      {
        path: 'atadigim-gorevler',
        canActivate: [roleGuard('Mentor')],
        title: 'Atadığım görevler · Pusula',
        loadComponent: () => import('./features/tasks/created-tasks.page').then((m) => m.CreatedTasksPage),
      },
      {
        path: 'yonetim/kullanicilar',
        canActivate: [roleGuard('Admin')],
        title: 'Kullanıcılar · Pusula',
        loadComponent: () => import('./features/admin/admin-users.page').then((m) => m.AdminUsersPage),
      },
      {
        path: 'yonetim/gruplar',
        canActivate: [roleGuard('Admin')],
        title: 'Tüm gruplar · Pusula',
        loadComponent: () => import('./features/admin/admin-groups.page').then((m) => m.AdminGroupsPage),
      },
      {
        path: 'yonetim/gorevler',
        canActivate: [roleGuard('Admin')],
        title: 'Tüm görevler · Pusula',
        loadComponent: () => import('./features/admin/admin-tasks.page').then((m) => m.AdminTasksPage),
      },
      {
        path: 'profil',
        title: 'Profilim · Pusula',
        loadComponent: () => import('./features/profile/profile.page').then((m) => m.ProfilePage),
      },
    ],
  },

  {
    path: '**',
    title: 'Sayfa bulunamadı · Pusula',
    loadComponent: () => import('./features/not-found.page').then((m) => m.NotFoundPage),
  },
];
