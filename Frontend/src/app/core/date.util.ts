// Tarih yardımcıları. Backend iki farklı türde tarih döner, ikisi de ayrı ele alınır:
//  1) "An" tarihleri (createdAt, joinedAt): UTC'dir, ama EF Core sonuna "Z" eklemeyebilir. Z yoksa UTC kabul ediyoruz,
//     aksi halde tarayıcı onu yerel saat sanıp saati kaydırırdı.
//  2) "Gün" tarihleri (dueDate, noteDate): kullanıcının seçtiği takvim günü. Saat dilimi dönüşümü YAPILMAMALI,
//     yoksa 10 Ekim bazı kullanıcılarda 9 Ekim görünür. Sadece "YYYY-MM-DD" kısmı kullanılır.

const ZONE_SUFFIX = /(Z|[+-]\d{2}:\d{2})$/;

export function parseUtc(value: string): Date {
  return new Date(ZONE_SUFFIX.test(value) ? value : value + 'Z');
}

export function formatDateTime(value: string): string {
  return parseUtc(value).toLocaleString('tr-TR', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** Gün tarihini yerel saatte gece yarısı olarak okur (saat dilimi kayması olmadan). */
export function parseDay(value: string): Date {
  const [y, m, d] = value.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d);
}

export function formatDay(value: string | null): string {
  if (!value) return '—';
  return parseDay(value).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });
}

/** "Pazartesi, 5 Ekim 2026" gibi gün adıyla uzun biçim (defter kayıtları için). */
export function formatDayLong(value: string | null): string {
  if (!value) return '—';
  return parseDay(value).toLocaleDateString('tr-TR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
}

/** Süreyi Türkçe biçimde yazar: 7.5 -> "7,5", 8 -> "8". */
export function formatHours(value: number | null): string {
  return value === null ? '' : value.toLocaleString('tr-TR', { maximumFractionDigits: 2 });
}

/** <input type="date"> bugünün değerini "YYYY-MM-DD" ister. */
export function todayInputValue(): string {
  const now = new Date();
  const mm = String(now.getMonth() + 1).padStart(2, '0');
  const dd = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${mm}-${dd}`;
}

/** Backend'den gelen gün tarihini <input type="date"> formatına çevirir. */
export function toInputDay(value: string | null): string {
  return value ? value.slice(0, 10) : '';
}

/** Input'tan gelen "YYYY-MM-DD" değerini backend'in DateTime alanına uygun gönderir; boşsa null. */
export function fromInputDay(value: string): string | null {
  return value ? `${value}T00:00:00` : null;
}

/** Bitiş günü geçmiş mi? (Bugün bitiyorsa gecikmiş sayılmaz.) */
export function isPastDay(value: string | null): boolean {
  if (!value) return false;
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return parseDay(value) < today;
}

/** "3 gün kaldı", "Bugün", "2 gün gecikti" gibi kısa ifade. */
export function relativeDay(value: string | null): string {
  if (!value) return '';
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const diff = Math.round((parseDay(value).getTime() - today.getTime()) / 86_400_000);
  if (diff === 0) return 'Bugün';
  if (diff === 1) return 'Yarın';
  return diff > 0 ? `${diff} gün kaldı` : `${-diff} gün gecikti`;
}
