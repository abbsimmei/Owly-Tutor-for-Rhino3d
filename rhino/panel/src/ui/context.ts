import type { PanelCommand } from '../protocol/events.js';
import type { Store } from '../state/store.js';
import type { UiState } from '../state/ui.js';
import type { Zoom } from '../state/zoom.js';

/** Everything a view needs: host state, local view state, and one way out to the host. */
export interface PanelContext {
  readonly store: Store;
  readonly ui: UiState;
  readonly zoom: Zoom;
  /** False when running against the mock in a plain browser. */
  readonly native: boolean;
  send(command: PanelCommand): void;
  copy(text: string): void;
  copyCommand(name: string): void;
  /** Plan 015: ask the host where a command is on the toolbars; the answer arrives as toolbar.found and fills the location popover. */
  findToolbar(name: string, turnId: string): void;
  /** Plan 021: ask the host for a command's toolbar button image, once per name. The answer fills store.icons. */
  requestToolbarIcon(name: string): void;
  /** Plan 022: ask the host whether a command may get a Run button, once per name. The answer fills store.runnable. */
  requestCommandCheck(name: string): void;
  /** Plan 023: ask the host for a command's options (from McNeel's help page), once per name. The answer fills store.commandOptions. */
  requestCommandOptions(name: string): void;
  /**
   * Plan 022 / ADR-0008: the student clicked Run. The host re-checks and starts the command; the AI cannot send this.
   * Plan 023: `options` holds only what the student set; the host rebuilds the macro from its own option data.
   */
  runCommand(name: string, options?: Readonly<Record<string, string>>): void;
  openLink(url: string): void;
  /** Sends the composer's draft, attachments and picked context as one prompt. */
  submit(text?: string): void;
}
