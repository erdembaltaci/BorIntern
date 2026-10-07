// Backend DTO'larının TypeScript karşılıkları. Alan adları ASP.NET'in varsayılan camelCase JSON çıktısıyla birebir aynı.

export type Role = 'Intern' | 'Mentor' | 'Admin';
export type UserStatus = 'Pending' | 'Active' | 'Inactive';
export type TaskStatus = 'Todo' | 'InProgress' | 'Completed';
/** Staj defteri kaydı yaşam döngüsü: Taslak -> Onay bekliyor -> Onaylandı / Düzeltme istendi. */
export type NoteStatus = 'Draft' | 'Submitted' | 'Approved' | 'ReturnedForRevision';

export interface User {
  id: number;
  fullName: string;
  email: string;
  role: Role;
  status: UserStatus;
  createdAt: string;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  user: User;
}

/** Backend'deki PagedResultDto<T>. */
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Group {
  id: number;
  name: string;
  mentorId: number;
  mentorName: string;
  createdAt: string;
}

export interface GroupMember {
  id: number;
  groupId: number;
  userId: number;
  fullName: string;
  email: string;
  joinedAt: string;
}

export interface TaskItem {
  id: number;
  title: string;
  description: string;
  status: TaskStatus;
  dueDate: string | null;
  assignedUserId: number;
  assignedUserName: string;
  createdByUserId: number;
  createdByUserName: string;
  createdAt: string;
}

export interface TaskSummary {
  totalTasks: number;
  todoCount: number;
  inProgressCount: number;
  completedCount: number;
  /** Bitiş günü geçmiş, tamamlanmamış görev sayısı (sadece kendi özetimde dolu). */
  overdueCount: number;
}

/** Staj defteri kaydı ("Yapılan iş" = content). */
export interface Note {
  id: number;
  title: string;
  content: string;
  learned: string;
  hoursSpent: number | null;
  /** Virgülle ayrılmış etiketler: "EF Core, JWT". */
  tags: string;
  noteDate: string | null;
  status: NoteStatus;
  submittedAt: string | null;
  mentorComment: string;
  reviewedAt: string | null;
  reviewedByName: string;
  userId: number;
  /** Kaydın sahibi (mentorun onay ekranında gösterilir). */
  userName: string;
  createdAt: string;
}

/** Defter kaydı oluşturma/güncelleme gövdesi. */
export interface NotePayload {
  title: string;
  content: string;
  learned: string;
  hoursSpent: number | null;
  tags: string;
  noteDate: string | null;
}

export interface CreateTaskRequest {
  title: string;
  description: string;
  dueDate: string | null;
  assignedUserId: number;
}

export interface Announcement {
  id: number;
  groupId: number;
  groupName: string;
  mentorId: number;
  mentorName: string;
  title: string;
  content: string;
  createdAt: string;
}

/** Yöneticinin bir kullanıcı için ürettiği parola sıfırlama bağlantısı (ham anahtar yalnızca bir kez gösterilir). */
export interface PasswordResetLink {
  userId: number;
  userName: string;
  link: string;
  expiresAt: string;
}
