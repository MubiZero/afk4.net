import type { SeatSummary, SeatTone } from './operatorData';

// Fallback ceiling for the prepaid time bar when we can't compute a real fraction: a full bar
// ≈ 60 min left, clamped to [0,1]. Used only when the session has no start instant (so we can't
// know its total length). With a start instant we scale against the real session length below.
export const SEAT_TIME_BAR_CEILING_SECONDS = 3600;
// Below this the bar turns "low" (urgent) so the operator notices a session about to end.
export const SEAT_TIME_LOW_SECONDS = 600;

// The lead metric a tile leads with, derived from the seat's live state. Drives both what the
// tile shows and where: postpaid sum sits up top (rising), prepaid time sits at the bottom with
// a depleting bar, a free seat invites with "+", everything else just states its status.
export type SeatTileLead =
  | { kind: 'free' }
  | { kind: 'postpaid'; amount: string }
  | { kind: 'prepaid'; remaining: string; barRatio: number; low: boolean; expired: boolean }
  | { kind: 'plain'; remaining: string };

// Loud accent is reserved for the one problem tone (offline = «нет связи»); calm states
// (ready/active/pending) stay quiet.
export function isAttentionTone(tone: SeatTone): boolean {
  return tone === 'offline';
}

export function seatTileLead(seat: SeatSummary, nowMs: number = Date.now()): SeatTileLead {
  const hasSession = seat.hasActiveSession === true || Boolean(seat.activeSessionId) || seat.tone === 'active';
  const seconds = seat.remainingSeconds ?? null;

  // Open tab / postpaid: a session with no countdown but an accruing cost → lead with the sum.
  if (hasSession && seconds === null && seat.accruedCostMinorUnits !== null && seat.accruedCostMinorUnits !== undefined) {
    return { kind: 'postpaid', amount: seat.remaining };
  }

  // Prepaid fixed session: time is counting down → lead with the remaining time + a depleting bar.
  // At (or past) zero the countdown has run out — the tile says "expired", not "0 left".
  if (hasSession && seconds !== null) {
    const barRatio = prepaidBarRatio(seat, seconds, nowMs);
    return { kind: 'prepaid', remaining: seat.remaining, barRatio, low: seconds <= SEAT_TIME_LOW_SECONDS, expired: seconds <= 0 };
  }

  // Free seat ready to seat someone → an inviting "+".
  if (!hasSession && seat.tone === 'ready') {
    return { kind: 'free' };
  }

  // Pending / offline (нет связи / сбой / обслуживание) without a session → plain status.
  return { kind: 'plain', remaining: seat.remaining };
}

// Доля заполнения полосы остатка. Когда известно начало сессии — это НАСТОЯЩИЙ процент:
// полная длина = прошло + осталось (продление само поднимет и остаток, и полную длину).
// Без начала сессии длины не знаем → фолбэк на «остаток от условного часа».
function prepaidBarRatio(seat: SeatSummary, seconds: number, nowMs: number): number {
  const remaining = Math.max(0, seconds);
  const startedMs = seat.sessionStartedAtUtc ? new Date(seat.sessionStartedAtUtc).getTime() : NaN;
  if (!Number.isNaN(startedMs)) {
    const elapsed = Math.max(0, (nowMs - startedMs) / 1000);
    const total = elapsed + remaining;
    if (total > 0) {
      return Math.max(0, Math.min(1, remaining / total));
    }
  }

  return Math.max(0, Math.min(1, remaining / SEAT_TIME_BAR_CEILING_SECONDS));
}
