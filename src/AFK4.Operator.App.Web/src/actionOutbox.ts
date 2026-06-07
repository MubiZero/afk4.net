import { type MessageKey } from '@afk4/i18n';
import type { SeatSummary } from './operatorData';

type TFn = (key: MessageKey, values?: Record<string, string | number>) => string;

// Local queue of operator lock/unlock intents recorded while the platform is unreachable (spec §6.5).
// Lock/unlock are idempotent device commands that reconcile against cloud authority, so they are safe to
// queue and replay on reconnect (unlike billing, which stays online-only per D2). localStorage mirror of
// the WPF FileActionOutbox, including dedupe-by-device (latest intent wins) and corrupt-self-heal.

export type OperatorCommandType = 'lock' | 'unlock';

export interface OperatorActionOutboxEntry {
  idempotencyKey: string;
  deviceId: string;
  seatId: string;
  seatName: string;
  commandType: OperatorCommandType;
  expectedSessionId: string | null;
  queuedAtMs: number;
}

export interface DroppedAction {
  entry: OperatorActionOutboxEntry;
  note: string;
}

export interface ReconcileResult {
  replay: OperatorActionOutboxEntry[];
  dropped: DroppedAction[];
}

const storageKey = 'afk4.operator.actionOutbox';

export function loadActionOutbox(): OperatorActionOutboxEntry[] {
  const raw = readItem(storageKey);
  if (raw === null) {
    return [];
  }

  try {
    const entries = JSON.parse(raw) as OperatorActionOutboxEntry[];
    return Array.isArray(entries) ? entries : [];
  } catch {
    removeItem(storageKey);
    return [];
  }
}

export function enqueueAction(entry: OperatorActionOutboxEntry): void {
  const pending = loadActionOutbox().filter((existing) => existing.deviceId !== entry.deviceId);
  pending.push(entry);
  persist(pending);
}

export function acknowledgeAction(idempotencyKey: string): void {
  const pending = loadActionOutbox();
  const next = pending.filter((existing) => existing.idempotencyKey !== idempotencyKey);
  if (next.length !== pending.length) {
    persist(next);
  }
}

// Splits queued actions into those safe to replay and those superseded by current cloud state (D9):
// the seat is gone, or its active session changed, so the queued intent no longer applies and is dropped
// with an operator-visible audit note rather than re-applied.
export function reconcileActionOutbox(
  pending: OperatorActionOutboxEntry[],
  liveSeats: SeatSummary[],
  t: TFn
): ReconcileResult {
  const replay: OperatorActionOutboxEntry[] = [];
  const dropped: DroppedAction[] = [];

  for (const entry of pending) {
    const seat = liveSeats.find((candidate) => candidate.deviceId === entry.deviceId);
    const currentSessionId = seat?.activeSessionId ?? null;
    if (!seat || currentSessionId !== entry.expectedSessionId) {
      dropped.push({
        entry,
        note: t('op.outbox.dropped', { seat: entry.seatName, command: entry.commandType })
      });
      continue;
    }

    replay.push(entry);
  }

  return { replay, dropped };
}

function persist(entries: OperatorActionOutboxEntry[]): void {
  try {
    localStorage.setItem(storageKey, JSON.stringify(entries));
  } catch {
    // Storage unavailable — the queue degrades to in-memory only for this session.
  }
}

function readItem(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function removeItem(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    // Best-effort cleanup; ignore storage failures.
  }
}
