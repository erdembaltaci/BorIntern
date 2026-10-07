import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_URL } from './config';
import { Announcement, CreateTaskRequest, PasswordResetLink, Group, GroupMember, Note, NotePayload, NoteStatus, Paged, Role, TaskItem, TaskStatus, TaskSummary, User } from './models';

/**
 * Backend uç noktalarının tamamı burada, tek yerde. Bileşenler URL bilmez, sadece "grubu getir" der.
 * Her metot Promise döner: bileşenlerde `await` ile okunması daha sade. Token ekleme/yenileme interceptor'ın işi.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  // ---- Profil -------------------------------------------------------------
  me(): Promise<User> {
    return this.get('/users/me');
  }
  updateMe(fullName: string): Promise<User> {
    return this.put('/users/me', { fullName });
  }

  // ---- Admin --------------------------------------------------------------
  adminUsers(page: number, pageSize: number, search = ''): Promise<Paged<User>> {
    return this.get('/admin/users', this.paging(page, pageSize, search));
  }
  adminPendingUsers(page: number, pageSize: number, search = ''): Promise<Paged<User>> {
    return this.get('/admin/users/pending', this.paging(page, pageSize, search));
  }
  /** Onaylama: Pending ya da Inactive kullanıcıyı Active yapar (backend durumu kontrol etmez). */
  approveUser(userId: number): Promise<User> {
    return this.post(`/admin/approve-user/${userId}`, {});
  }
  deactivateUser(userId: number): Promise<User> {
    return this.post(`/admin/deactivate-user/${userId}`, {});
  }
  /**
   * E-posta olmadan parola sıfırlama: bağlantı yöneticiye döner, yönetici kullanıcıya güvenli bir kanaldan iletir.
   * Bağlantı tek kullanımlık ve 24 saat geçerli; ham anahtar bir daha gösterilemez, önceki bağlantılar geçersiz olur.
   */
  createResetLink(userId: number): Promise<PasswordResetLink> {
    return this.post(`/admin/users/${userId}/reset-link`, {});
  }
  changeUserRole(userId: number, role: Role): Promise<User> {
    return this.put(`/admin/users/${userId}/role`, { role });
  }
  adminGroups(page: number, pageSize: number, search = ''): Promise<Paged<Group>> {
    return this.get('/admin/groups', this.paging(page, pageSize, search));
  }
  adminTasks(page: number, pageSize: number, search = ''): Promise<Paged<TaskItem>> {
    return this.get('/admin/tasks', this.paging(page, pageSize, search));
  }

  // ---- Gruplar (Mentor) ---------------------------------------------------
  createGroup(name: string): Promise<Group> {
    return this.post('/groups', { name });
  }
  myGroups(page: number, pageSize: number, search = ''): Promise<Paged<Group>> {
    return this.get('/groups/mine', this.paging(page, pageSize, search));
  }
  group(groupId: number): Promise<Group> {
    return this.get(`/groups/${groupId}`);
  }
  updateGroup(groupId: number, name: string): Promise<Group> {
    return this.put(`/groups/${groupId}`, { name });
  }
  deleteGroup(groupId: number): Promise<void> {
    return this.delete(`/groups/${groupId}`);
  }
  restoreGroup(groupId: number): Promise<Group> {
    return this.post(`/groups/${groupId}/restore`, {});
  }
  groupMembers(groupId: number, page: number, pageSize: number): Promise<Paged<GroupMember>> {
    return this.get(`/groups/${groupId}/members`, this.paging(page, pageSize));
  }
  addGroupMember(groupId: number, userId: number): Promise<GroupMember> {
    return this.post(`/groups/${groupId}/members`, { userId });
  }
  removeGroupMember(groupId: number, userId: number): Promise<void> {
    return this.delete(`/groups/${groupId}/members/${userId}`);
  }

  /** Mentor: SADECE kendi gruplarındaki aktif stajyerleri arar (görev devri için). */
  searchMyInterns(search: string, page: number, pageSize: number): Promise<Paged<User>> {
    return this.get('/users/my-interns', this.paging(page, pageSize, search));
  }
  /** Mentor: gruba eklemek için aktif stajyerleri ad/e-posta ile arar. */
  searchInterns(search: string, page: number, pageSize: number): Promise<Paged<User>> {
    return this.get('/users/interns', this.paging(page, pageSize).set('search', search));
  }

  // ---- Görevler -----------------------------------------------------------
  createTask(request: CreateTaskRequest): Promise<TaskItem> {
    return this.post('/tasks', request);
  }
  /** status verilirse sadece o durumdaki görevler gelir (pano sütunları ve liste filtresi için sunucuda süzülür). */
  myTasks(page: number, pageSize: number, status?: TaskStatus): Promise<Paged<TaskItem>> {
    const params = this.paging(page, pageSize);
    return this.get('/tasks/mine', status ? params.set('status', status) : params);
  }
  /** Durum sayıları + geciken görev sayısı (görevleri çekmeden, veritabanında sayılır). */
  mySummary(): Promise<TaskSummary> {
    return this.get('/tasks/mine/summary');
  }
  /** Panel için: bitiş tarihi en yakın bitmemiş görevler. */
  myUpcoming(take: number): Promise<TaskItem[]> {
    return this.get('/tasks/mine/upcoming', new HttpParams().set('take', take));
  }
  /** Mentor: kendi atadığı görevler (hangi stajyere olursa olsun). */
  createdTasks(page: number, pageSize: number, search = ''): Promise<Paged<TaskItem>> {
    return this.get('/tasks/created', this.paging(page, pageSize, search));
  }
  updateTaskStatus(taskId: number, status: TaskStatus): Promise<TaskItem> {
    return this.put(`/tasks/${taskId}/status`, { status });
  }
  deleteTask(taskId: number): Promise<void> {
    return this.delete(`/tasks/${taskId}`);
  }
  restoreTask(taskId: number): Promise<TaskItem> {
    return this.post(`/tasks/${taskId}/restore`, {});
  }
  /** Mentor: atadığı görevin bilgilerini düzenler; assignedUserId değişirse görev başka stajyere devredilir. */
  updateTask(taskId: number, request: CreateTaskRequest): Promise<TaskItem> {
    return this.put(`/tasks/${taskId}`, request);
  }
  taskSummary(userId: number): Promise<TaskSummary> {
    return this.get(`/tasks/summary/${userId}`);
  }

  // ---- Duyurular ----------------------------------------------------------
  /** Mentor: kendi grubuna duyuru gönderir. */
  createAnnouncement(groupId: number, title: string, content: string): Promise<Announcement> {
    return this.post(`/groups/${groupId}/announcements`, { title, content });
  }
  groupAnnouncements(groupId: number, page: number, pageSize: number): Promise<Paged<Announcement>> {
    return this.get(`/groups/${groupId}/announcements`, this.paging(page, pageSize));
  }
  /** Üyesi olduğum tüm grupların duyuruları (stajyerin duyuru sayfası). */
  myAnnouncements(page: number, pageSize: number): Promise<Paged<Announcement>> {
    return this.get('/announcements/mine', this.paging(page, pageSize));
  }
  deleteAnnouncement(groupId: number, announcementId: number): Promise<void> {
    return this.delete(`/groups/${groupId}/announcements/${announcementId}`);
  }

  // ---- Staj defteri -------------------------------------------------------
  createNote(payload: NotePayload): Promise<Note> {
    return this.post('/notes', payload);
  }
  /** Kendi kayıtlarım: durum ve arama sunucuda süzülür, tarihe göre en yeni önce. */
  myNotes(page: number, pageSize: number, status?: NoteStatus, search = ''): Promise<Paged<Note>> {
    let params = this.paging(page, pageSize, search);
    if (status) params = params.set('status', status);
    return this.get('/notes/mine', params);
  }
  updateNote(noteId: number, payload: NotePayload): Promise<Note> {
    return this.put(`/notes/${noteId}`, payload);
  }
  deleteNote(noteId: number): Promise<void> {
    return this.delete(`/notes/${noteId}`);
  }
  restoreNote(noteId: number): Promise<Note> {
    return this.post(`/notes/${noteId}/restore`, {});
  }
  /** Stajyer: kaydı mentora gönderir / gönderileni geri çeker. */
  submitNote(noteId: number): Promise<Note> {
    return this.post(`/notes/${noteId}/submit`, {});
  }
  withdrawNote(noteId: number): Promise<Note> {
    return this.post(`/notes/${noteId}/withdraw`, {});
  }
  /** Mentor: kendi gruplarındaki stajyerlerin gönderilmiş kayıtları (taslaklar görünmez). */
  reviewQueue(page: number, pageSize: number, status?: NoteStatus, search = ''): Promise<Paged<Note>> {
    let params = this.paging(page, pageSize, search);
    if (status) params = params.set('status', status);
    return this.get('/notes/review', params);
  }
  /** Mentor: kaydı onaylar ya da (açıklamayla) düzeltme ister. */
  reviewNote(noteId: number, approve: boolean, comment: string): Promise<Note> {
    return this.post(`/notes/${noteId}/review`, { approve, comment });
  }
  /** Yazdırılabilir defter için tarih aralığındaki kayıtlar (eskiden yeniye, en fazla 400). */
  exportNotes(from?: string, to?: string): Promise<Note[]> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.get('/notes/export', params);
  }

  // ---- Yardımcılar --------------------------------------------------------

  /** Sayfalama parametreleri; arama metni doluysa o da eklenir (boşsa hiç gönderilmez). */
  private paging(page: number, pageSize: number, search = ''): HttpParams {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return search.trim() ? params.set('search', search.trim()) : params;
  }
  private get<T>(path: string, params?: HttpParams): Promise<T> {
    return firstValueFrom(this.http.get<T>(API_URL + path, { params }));
  }
  private post<T>(path: string, body: unknown): Promise<T> {
    return firstValueFrom(this.http.post<T>(API_URL + path, body));
  }
  private put<T>(path: string, body: unknown): Promise<T> {
    return firstValueFrom(this.http.put<T>(API_URL + path, body));
  }
  private delete<T = void>(path: string): Promise<T> {
    return firstValueFrom(this.http.delete<T>(API_URL + path));
  }
}
