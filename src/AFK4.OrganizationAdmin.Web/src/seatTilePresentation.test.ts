import { describe, expect, it } from 'bun:test';
import type { SeatSummary, SeatTone } from './operatorData';
import {
  SEAT_TIME_BAR_CEILING_SECONDS,
  SEAT_TIME_LOW_SECONDS,
  isAttentionTone,
  seatTileLead
} from './seatTilePresentation';

function seat(overrides: Partial<SeatSummary>): SeatSummary {
  return {
    id: 's',
    zone: 'Зал A',
    name: 'PC-01',
    tone: 'ready',
    stateLabel: 'Свободно',
    player: 'Гость',
    remaining: 'Свободно',
    device: '',
    command: '',
    app: '',
    ...overrides
  };
}

describe('isAttentionTone', () => {
  it('marks only the offline («нет связи») tone as loud; service stays calm', () => {
    const loud: SeatTone[] = ['offline'];
    const calm: SeatTone[] = ['ready', 'active', 'pending', 'service'];
    for (const tone of loud) {
      expect(isAttentionTone(tone)).toBe(true);
    }
    for (const tone of calm) {
      expect(isAttentionTone(tone)).toBe(false);
    }
  });
});

describe('seatTileLead', () => {
  it('shows a "+" affordance for a free ready seat with no session', () => {
    const lead = seatTileLead(seat({ tone: 'ready', hasActiveSession: false }));
    expect(lead.kind).toBe('free');
  });

  it('shows the accruing amount for an open tab (session, no countdown, cost accruing)', () => {
    const lead = seatTileLead(
      seat({ tone: 'active', hasActiveSession: true, remainingSeconds: null, accruedCostMinorUnits: 5400, remaining: '≈ 54 с.' })
    );
    expect(lead).toEqual({ kind: 'postpaid', amount: '≈ 54 с.' });
  });

  it('falls back to the ceiling ratio when the session has no start instant', () => {
    const half = Math.round(SEAT_TIME_BAR_CEILING_SECONDS / 2);
    const lead = seatTileLead(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: half, remaining: '30 мин' }));
    expect(lead.kind).toBe('prepaid');
    if (lead.kind === 'prepaid') {
      expect(lead.remaining).toBe('30 мин');
      expect(lead.barRatio).toBeCloseTo(0.5, 5);
      expect(lead.low).toBe(false);
    }
  });

  it('scales the bar to the REAL session length when the start instant is known', () => {
    const startedAtUtc = '2026-06-19T12:00:00Z';
    const startedMs = new Date(startedAtUtc).getTime();
    // 90 мин прошло, 30 мин осталось → полная сессия 120 мин → доля 0.25,
    // а не 0.5 «от условного часа» (так старый костыль соврал бы).
    const lead = seatTileLead(
      seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 1800, sessionStartedAtUtc: startedAtUtc }),
      startedMs + 90 * 60_000
    );
    expect(lead.kind).toBe('prepaid');
    if (lead.kind === 'prepaid') {
      expect(lead.barRatio).toBeCloseTo(0.25, 5);
    }
  });

  it('clamps the bar ratio to [0,1] and flags low time near the end', () => {
    const over = seatTileLead(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: SEAT_TIME_BAR_CEILING_SECONDS * 3 }));
    const low = seatTileLead(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: SEAT_TIME_LOW_SECONDS - 1 }));
    if (over.kind === 'prepaid') {
      expect(over.barRatio).toBe(1);
      expect(over.expired).toBe(false);
    }
    if (low.kind === 'prepaid') {
      expect(low.low).toBe(true);
      expect(low.barRatio).toBeGreaterThan(0);
      expect(low.expired).toBe(false);
    }
  });

  it('flags a prepaid session whose countdown hit zero as expired (not "0 left")', () => {
    const lead = seatTileLead(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 0 }));
    expect(lead.kind).toBe('prepaid');
    if (lead.kind === 'prepaid') {
      expect(lead.expired).toBe(true);
      expect(lead.low).toBe(true);
      expect(lead.barRatio).toBe(0);
    }
  });

  it('falls back to plain status text for offline/pending and other non-session seats', () => {
    expect(seatTileLead(seat({ tone: 'offline', remaining: 'Нет связи с ПК' }))).toEqual({ kind: 'plain', remaining: 'Нет связи с ПК' });
    expect(seatTileLead(seat({ tone: 'pending', remaining: 'Ожидает' }))).toEqual({ kind: 'plain', remaining: 'Ожидает' });
  });
});
