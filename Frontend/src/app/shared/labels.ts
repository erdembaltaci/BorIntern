import { NoteStatus, Role, TaskStatus, UserStatus } from '../core/models';

// Backend enum değerleri (İngilizce) ile ekranda görünen Türkçe metinler burada eşleşir.

export const ROLE_LABEL: Record<Role, string> = {
  Intern: 'Stajyer',
  Mentor: 'Mentor',
  Admin: 'Yönetici',
};

export const USER_STATUS_LABEL: Record<UserStatus, string> = {
  Pending: 'Onay bekliyor',
  Active: 'Aktif',
  Inactive: 'Pasif',
};

export const TASK_STATUS_LABEL: Record<TaskStatus, string> = {
  Todo: 'Yapılacak',
  InProgress: 'Devam ediyor',
  Completed: 'Tamamlandı',
};

export const NOTE_STATUS_LABEL: Record<NoteStatus, string> = {
  Draft: 'Taslak',
  Submitted: 'Onay bekliyor',
  Approved: 'Onaylandı',
  ReturnedForRevision: 'Düzeltme istendi',
};

export const NOTE_STATUSES: NoteStatus[] = ['Draft', 'Submitted', 'Approved', 'ReturnedForRevision'];

export const TASK_STATUSES: TaskStatus[] = ['Todo', 'InProgress', 'Completed'];
export const ROLES: Role[] = ['Intern', 'Mentor', 'Admin'];

/** Ad soyaddan avatar harfleri: "Ali Erdem Baltacı" -> "AB". */
export function initials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].charAt(0);
  return parts[0].charAt(0) + parts[parts.length - 1].charAt(0);
}
