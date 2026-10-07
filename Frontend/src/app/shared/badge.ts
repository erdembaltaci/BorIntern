import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NOTE_STATUS_LABEL, ROLE_LABEL, TASK_STATUS_LABEL, USER_STATUS_LABEL } from './labels';

type BadgeKind = 'role' | 'user' | 'task' | 'note';

interface BadgeStyle {
  label: string;
  tone: 'neutral' | 'primary' | 'success' | 'warning' | 'danger' | 'info';
}

/** Rol / kullanıcı durumu / görev durumu için renkli rozet. Kullanım: <app-badge kind="task" [value]="task.status" /> */
@Component({
  selector: 'app-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="'badge badge-' + style().tone">{{ style().label }}</span>`,
})
export class Badge {
  readonly kind = input.required<BadgeKind>();
  readonly value = input.required<string>();

  protected readonly style = computed<BadgeStyle>(() => {
    const value = this.value();
    switch (this.kind()) {
      case 'role':
        return {
          label: ROLE_LABEL[value as keyof typeof ROLE_LABEL] ?? value,
          tone: value === 'Admin' ? 'danger' : value === 'Mentor' ? 'info' : 'primary',
        };
      case 'user':
        return {
          label: USER_STATUS_LABEL[value as keyof typeof USER_STATUS_LABEL] ?? value,
          tone: value === 'Active' ? 'success' : value === 'Pending' ? 'warning' : 'neutral',
        };
      case 'note':
        return {
          label: NOTE_STATUS_LABEL[value as keyof typeof NOTE_STATUS_LABEL] ?? value,
          tone: value === 'Approved' ? 'success' : value === 'Submitted' ? 'info' : value === 'ReturnedForRevision' ? 'warning' : 'neutral',
        };
      case 'task':
        return {
          label: TASK_STATUS_LABEL[value as keyof typeof TASK_STATUS_LABEL] ?? value,
          tone: value === 'Completed' ? 'success' : value === 'InProgress' ? 'info' : 'neutral',
        };
    }
  });
}
