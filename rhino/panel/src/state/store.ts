// Host state, held as signals all the way down to the individual streaming text block.
//
// The point of putting signals inside the transcript model rather than around it: a `turn.text`
// delta calls `block.text.set(...)` and exactly one DOM text node updates. Nothing re-flattens,
// nothing re-diffs, no row is rebuilt, no width is re-measured.

import { computed, signal, type ReadSignal, type Signal } from '../core/signal.js';
import { applyStrings } from '../i18n/t.js';
import {
  type AgentInfo,
  type Attachment,
  type BlockSnapshot,
  type ContextItem,
  type HostEvent,
  type HostInfo,
  type NoticeLevel,
  type PendingQuestion,
  type PlanStep,
  type ProjectInfo,
  type TokenUsage,
  type ToolCall,
  type TurnImage,
  type TurnSnapshot,
  type ToolbarPlace,
  type TurnStatus,
} from '../protocol/events.js';

/** Plan 020: the one open "Where to find" popover. `places` stays undefined while the host is still looking. */
export interface LocationView {
  turnId: string;
  name: string;
  places?: readonly ToolbarPlace[];
}

export type BlockView =
  | { kind: 'text'; id: string; at: string; text: Signal<string> }
  | { kind: 'tool'; id: string; call: Signal<ToolCall> }
  | { kind: 'image'; id: string; image: TurnImage }
  | { kind: 'notice'; id: string; level: NoticeLevel; text: string };

export interface TurnView {
  id: string;
  prompt: string;
  attachments: readonly Attachment[];
  context: readonly ContextItem[];
  startedAt: string;
  status: Signal<TurnStatus>;
  usage: Signal<TokenUsage | null>;
  blocks: Signal<readonly BlockView[]>;
  plan: Signal<readonly PlanStep[]>;
  undoable: Signal<boolean>;
  error: Signal<string | undefined>;
}

export interface SessionView {
  sessionId: string;
  docTitle: string;
  startedAt: string;
}

/** Plan 025: this document's project, as the host last reported it. */
export interface ProjectView {
  id: string | null;
  name: string | null;
  pending: boolean;
  suggestion: string | null;
  projects: readonly ProjectInfo[];
}

export interface Notice {
  id: string;
  level: NoticeLevel;
  text: string;
  /** Bumped when the same notice fires again, which restarts its on-screen lifetime. */
  repeats: Signal<number>;
}

function blockFrom(snapshot: BlockSnapshot): BlockView {
  switch (snapshot.kind) {
    case 'text':
      return { kind: 'text', id: snapshot.id, at: snapshot.at, text: signal(snapshot.text) };
    case 'tool':
      return { kind: 'tool', id: snapshot.id, call: signal(snapshot.call) };
    case 'image':
      return { kind: 'image', id: snapshot.id, image: snapshot.image };
    case 'notice':
      return { kind: 'notice', id: snapshot.id, level: snapshot.level, text: snapshot.text };
  }
}

function turnFrom(snapshot: TurnSnapshot): TurnView {
  return {
    id: snapshot.id,
    prompt: snapshot.prompt,
    attachments: snapshot.attachments,
    context: snapshot.context,
    startedAt: snapshot.startedAt,
    status: signal(snapshot.status),
    usage: signal(snapshot.usage),
    blocks: signal<readonly BlockView[]>(snapshot.blocks.map(blockFrom)),
    plan: signal<readonly PlanStep[]>(snapshot.plan),
    undoable: signal(snapshot.undoable),
    error: signal(snapshot.error),
  };
}

let noticeSeq = 0;

export class Store {
  readonly host = signal<HostInfo | null>(null);
  readonly scheme = signal<'light' | 'dark'>('dark');
  readonly agents = signal<readonly AgentInfo[]>([]);
  readonly activeAgentName = signal<string | null>(null);
  readonly context = signal<readonly ContextItem[]>([]);
  readonly session = signal<SessionView | null>(null);
  readonly turns = signal<readonly TurnView[]>([]);
  readonly questions = signal<readonly PendingQuestion[]>([]);
  /** Plan 020: the one open location popover. It belongs to the conversation: cleared with it, never kept (ADR-0006). */
  readonly location = signal<LocationView | null>(null);
  // Answers for this conversation, so a second click on the same command opens at once.
  private readonly cache = new Map<string, readonly ToolbarPlace[]>();
  // Names the panel has asked the host about; an answer nobody asked for opens nothing.
  private readonly asked = new Set<string>();
  /**
   * Plan 021: button images by chip name (`_Loft`), as base64 SVG. Not conversation content, so a new
   * conversation keeps them. An entry with neither variant means the host looked and found none.
   */
  readonly icons = signal<ReadonlyMap<string, { light?: string; dark?: string }>>(new Map());
  private readonly iconAsked = new Set<string>();
  /**
   * Plan 022: whether the host lets Run start a command, by chip name. Host-decided and kept across conversations,
   * like icons. No per-card state lives here (plan 023 adds the options).
   */
  readonly runnable = signal<ReadonlyMap<string, 'ok' | 'blocked' | 'unknown'>>(new Map());
  private readonly checkAsked = new Set<string>();
  readonly notices = signal<readonly Notice[]>([]);
  readonly status = signal<string | null>(null);
  readonly project = signal<ProjectView | null>(null);

  readonly activeAgent: ReadSignal<AgentInfo | null> = computed(() => {
    const name = this.activeAgentName();
    return this.agents().find((agent) => agent.name === name) ?? null;
  });

  readonly hasReadyAgent: ReadSignal<boolean> = computed(() =>
    this.agents().some((agent) => agent.availability === 'ready'),
  );

  readonly currentTurn: ReadSignal<TurnView | null> = computed(() => {
    const list = this.turns();
    return list.length > 0 ? (list[list.length - 1] as TurnView) : null;
  });

  readonly running: ReadSignal<boolean> = computed(() => this.currentTurn()?.status() === 'running');

  /** The agent is not thinking while it is blocked on unanswered questions. */
  readonly thinking: ReadSignal<boolean> = computed(() => this.running() && this.questions().length === 0);


  apply(event: HostEvent): void {
    switch (event.type) {
      case 'hello':
        this.host.set(event.host);
        applyStrings(event.language, event.strings);
        // Scrollbar styling is Windows-only; see panel.css.
        document.documentElement.dataset['platform'] = event.host.platform;
        return;

      case 'theme':
        this.scheme.set(event.scheme);
        if (event.tokens)
          for (const [name, value] of Object.entries(event.tokens))
            document.documentElement.style.setProperty(`--${name}`, value);
        return;

      case 'agents':
        this.agents.set(event.agents);
        this.activeAgentName.set(event.active);
        return;

      case 'context':
        this.context.set(event.items);
        return;

      case 'conversation':
        this.session.set({
          sessionId: event.snapshot.sessionId,
          docTitle: event.snapshot.docTitle,
          startedAt: event.snapshot.startedAt,
        });
        this.activeAgentName.set(event.snapshot.agent);
        this.turns.set(event.snapshot.turns.map(turnFrom));
        this.questions.set([]);
        this.location.set(null);
        this.cache.clear();
        this.asked.clear();
        return;

      // Idempotent on id. A host that re-announces a turn or a call must not be able to put two
      // rows with the same key into the transcript: the keyed list would track one and orphan the
      // other in the DOM, where nothing can ever remove it.
      case 'turn.begin':
        if (this.turns().some((turn) => turn.id === event.turn.id)) return;
        this.turns.set((turns) => [...turns, turnFrom(event.turn)]);
        return;

      case 'turn.text': {
        const turn = this.turn(event.turnId);
        if (!turn) return;
        const existing = turn
          .blocks()
          .find((block): block is Extract<BlockView, { kind: 'text' }> =>
            block.kind === 'text' && block.id === event.blockId,
          );
        if (existing) {
          existing.text.set((text) => text + event.delta);
          return;
        }
        turn.blocks.set((blocks) => [
          ...blocks,
          { kind: 'text', id: event.blockId, at: new Date().toISOString(), text: signal(event.delta) },
        ]);
        return;
      }

      case 'turn.tool': {
        const turn = this.turn(event.turnId);
        if (!turn) return;
        if (turn.blocks().some((block) => block.id === event.call.id)) return;
        turn.blocks.set((blocks) => [
          ...blocks,
          { kind: 'tool', id: event.call.id, call: signal(event.call) },
        ]);
        return;
      }

      case 'turn.tool.patch': {
        const block = this.turn(event.turnId)
          ?.blocks()
          .find((b): b is Extract<BlockView, { kind: 'tool' }> => b.kind === 'tool' && b.id === event.callId);
        block?.call.set((call) => ({ ...call, ...event.patch }));
        return;
      }

      // A replay re-announces every image under the same id, so the second one is the row already drawn.
      case 'turn.image': {
        const turn = this.turn(event.turnId);
        if (!turn) return;
        if (turn.blocks().some((block) => block.id === event.image.id)) return;
        turn.blocks.set((blocks) => [...blocks, { kind: 'image', id: event.image.id, image: event.image }]);
        return;
      }

      case 'turn.plan':
        this.turn(event.turnId)?.plan.set(event.steps);
        return;

      case 'turn.usage':
        this.turn(event.turnId)?.usage.set(event.usage);
        return;

      case 'turn.end': {
        const turn = this.turn(event.turnId);
        if (!turn) return;
        turn.status.set(event.status);
        turn.error.set(event.error);
        return;
      }

      // Appended, not replaced: a second ask_user adds to the stack rather than evicting what is
      // already showing. Idempotent on id for the same reason turn.begin is.
      case 'question':
        this.questions.set((list) =>
          list.some((q) => q.id === event.question.id) ? list : [...list, event.question],
        );
        return;

      case 'question.clear':
        this.questions.set((list) => list.filter((q) => q.id !== event.id));
        return;

      // Plan 020: an answer to one chip click. A turn that is already gone (a race with New conversation) and
      // an answer nobody asked for are both ignored.
      case 'toolbar.found': {
        if (!this.turn(event.turnId) || !this.asked.has(event.name)) return;
        this.cache.set(event.name, event.places);
        const open = this.location.peek();
        if (open && open.name === event.name) this.location.set({ ...open, places: event.places });
        return;
      }

      // Plan 021: always kept, asked for or not. It is harmless data.
      case 'toolbar.icon.found': {
        const entry: { light?: string; dark?: string } = {};
        if (event.light) entry.light = event.light;
        if (event.dark) entry.dark = event.dark;
        this.icons.set((map) => new Map(map).set(event.name, entry));
        return;
      }

      // Plan 022: always kept, asked for or not.
      case 'command.checked':
        this.runnable.set((map) => new Map(map).set(event.name, event.run));
        return;

      // The same remark twice is still one remark: a repeat bumps the toast already showing.
      case 'notice': {
        const showing = this.notices().find((n) => n.level === event.level && n.text === event.text);
        if (showing) {
          showing.repeats.set((count) => count + 1);
          return;
        }
        const notice: Notice = {
          id: `notice-${++noticeSeq}`,
          level: event.level,
          text: event.text,
          repeats: signal(0),
        };
        this.notices.set((list) => [...list, notice]);
        return;
      }

      case 'status':
        this.status.set(event.text);
        return;

      case 'project':
        this.project.set({
          id: event.id ?? null,
          name: event.name ?? null,
          pending: event.pending,
          suggestion: event.suggestion ?? null,
          projects: event.projects,
        });
        return;
    }
  }

  /** Opens the popover. True means the host has to be asked; the caller sends toolbar.find. */
  openLocation(turnId: string, name: string): boolean {
    const places = this.cache.get(name);
    this.location.set(places ? { turnId, name, places } : { turnId, name });
    if (places) return false;
    this.asked.add(name);
    return true;
  }

  /** True the first time a name needs asking; false once it is asked or known. Streaming re-renders call this every frame. */
  markIconAsked(name: string): boolean {
    if (this.iconAsked.has(name) || this.icons.peek().has(name)) return false;
    this.iconAsked.add(name);
    return true;
  }

  /** True the first time a command needs checking with the host; false once asked or known. */
  markCheckAsked(name: string): boolean {
    if (this.checkAsked.has(name) || this.runnable.peek().has(name)) return false;
    this.checkAsked.add(name);
    return true;
  }

  /** A data: URL for the chip's button image in the current scheme, or null when there is none. */
  iconUrl(name: string): string | null {
    const entry = this.icons().get(name);
    if (!entry) return null;
    const svg = this.scheme() === 'dark' ? (entry.dark ?? entry.light) : (entry.light ?? entry.dark);
    return svg ? `data:image/svg+xml;base64,${svg}` : null;
  }

  closeLocation(): void {
    this.location.set(null);
  }

  dismissNotice(id: string): void {
    this.notices.set((list) => list.filter((notice) => notice.id !== id));
  }

  private turn(id: string): TurnView | undefined {
    return this.turns().find((turn) => turn.id === id);
  }
}
