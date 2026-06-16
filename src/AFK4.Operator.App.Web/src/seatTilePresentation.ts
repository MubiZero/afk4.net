import type { SeatSummary, SeatTone } from './operatorData';

// Reference ceiling for the prepaid time bar: a full bar ≈ 60 min left, clamped to [0,1].
// This is a glanceable "how much time is left" magnitude, NOT a percent-of-session — the
// floor-map DTO carries no session total, so we deliberately scale against a fixed reference
// instead of inventing a denominator. The bar shrinks toward 0 as the session runs out.
export const SEAT_TIME_BAR_CEILING_SECONDS = 3600;
// Below this the bar turns "low" (urgent) so the operator notices a session about to end.
export const SEAT_TIME_LOW_SECONDS = 600;

// The lead metric a tile leads with, derived from the seat's live state. Drives both what the
// tile shows and where: postpaid sum sits up top (rising), prepaid time sits at the bottom with
// a depleting bar, a free seat invites with "+", everything else just states its status.
export type SeatTileLead =
  | { kind: 'free' }
  | { kind: 'postpaid'; amount: string }
  | { kind: 'prepaid'; remaining: string; barRatio: number; low: boolean }
  | { kind: 'plain'; remaining: string };

// Loud colour is reserved for Attention/Problem; calm states (ready/active/pending) stay quiet.
export function isAttentionTone(tone: SeatTone): boolean {
  return tone === 'blocking' || tone === 'offline';
}

export function seatTileLead(seat: SeatSummary): SeatTileLead {
  const hasSession = seat.hasActiveSession === true || Boolean(seat.activeSessionId) || seat.tone === 'active';
  const seconds = seat.remainingSeconds ?? null;

  // Open tab / postpaid: a session with no countdown but an accruing cost → lead with the sum.
  if (hasSession && seconds === null && seat.accruedCostMinorUnits !== null && seat.accruedCostMinorUnits !== undefined) {
    return { kind: 'postpaid', amount: seat.remaining };
  }

  // Prepaid fixed session: time is counting down → lead with the remaining time + a depleting bar.
  if (hasSession && seconds !== null) {
    const barRatio = Math.max(0, Math.min(1, seconds / SEAT_TIME_BAR_CEILING_SECONDS));
    return { kind: 'prepaid', remaining: seat.remaining, barRatio, low: seconds <= SEAT_TIME_LOW_SECONDS };
  }

  // Free seat ready to seat someone → an inviting "+".
  if (!hasSession && seat.tone === 'ready') {
    return { kind: 'free' };
  }

  // Pending / offline / service / blocking-without-session, or active-but-offline → plain status.
  return { kind: 'plain', remaining: seat.remaining };
}
