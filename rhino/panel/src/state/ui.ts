// Purely local view state: nothing here needs to survive a reload or reach the host.

import { computed, signal, type ReadSignal } from '../core/signal.js';
import type { Attachment } from '../protocol/events.js';

export type Overlay = 'none' | 'agents' | 'context' | 'project' | 'location';

export class UiState {
  readonly draft = signal('');
  readonly attachments = signal<readonly Attachment[]>([]);
  readonly overlay = signal<Overlay>('none');
  /** False once the user scrolls away from the tail; new output then shows a jump affordance. */
  readonly pinned = signal(true);
  /** Output landed while the user was scrolled away from the tail. */
  readonly hasNew = signal(false);

  readonly hasDraft: ReadSignal<boolean> = computed(
    () => this.draft().trim().length > 0 || this.attachments().length > 0,
  );

  openOverlay(overlay: Overlay): void {
    this.overlay.set((current) => (current === overlay ? 'none' : overlay));
  }

  closeOverlay(): void {
    this.overlay.set('none');
  }

  addAttachment(attachment: Attachment): void {
    this.attachments.set((list) => [...list, attachment]);
  }

  removeAttachment(id: string): void {
    this.attachments.set((list) => list.filter((attachment) => attachment.id !== id));
  }

  clearComposer(): void {
    this.draft.set('');
    this.attachments.set([]);
  }
}
