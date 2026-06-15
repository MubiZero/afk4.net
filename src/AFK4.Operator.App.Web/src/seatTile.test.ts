import { describe, expect, it } from 'bun:test';
import type { SeatSummary, SeatTone } from './operatorData';
import {
  SEAT_TIME_BAR_CEILING_SECONDS,
  SEAT_TIME_LOW_SECONDS,
  isAttentionTone,
  seatTileLead
} from './seatTile';

function seat(overrides: Partial<SeatSummary>): SeatSummary {
  return {
    id: 's',
    zone: 'Зал A',
    name: 'PC-01',
    tone: 'ready',
    stateLabel: 'Свободно',
    player: 'Гость',
    remaining: 'Свободно',
    billing: 'Fast guest',
    device: '',
    command: '',
    app: '',
    ...overrides
  };
}

describe('isAttentionTone', () => {
  it('marks only attention/problem tones as loud', () => {
    const loud: SeatTone[] = ['warning', 'blocking', 'offline', 'service'];
    const calm: SeatTone[] = ['ready', 'active', 'pending'];
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

  it('shows time + bar for a prepaid fixed session, bar scaled to the ceiling', () => {
    const half = Math.round(SEAT_TIME_BAR_CEILING_SECONDS / 2);
    const lead = seatTileLead(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: half, remaining: '30 мин' }));
    expect(lead.kind).toBe('prepaid');
    if (lead.kind === 'prepaid') {
      expect(lead.remaining).toBe('30 мин');
      expect(lead.barRatio).toBeCloseTo(0.5, 5);
      expect(lead.low).toBe(false);
    }
  });

  it('clamps the bar ratio to [0,1] and flags low time near the end', () => {
    const over = seatTileLead(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: SEAT_TIME_BAR_CEILING_SECONDS * 3 }));
    const low = seatTileLead(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: SEAT_TIME_LOW_SECONDS - 1 }));
    if (over.kind === 'prepaid') {
      expect(over.barRatio).toBe(1);
    }
    if (low.kind === 'prepaid') {
      expect(low.low).toBe(true);
      expect(low.barRatio).toBeGreaterThan(0);
    }
  });

  it('falls back to plain status text for offline/service/pending and other non-session seats', () => {
    expect(seatTileLead(seat({ tone: 'offline', remaining: 'Нет heartbeat' }))).toEqual({ kind: 'plain', remaining: 'Нет heartbeat' });
    expect(seatTileLead(seat({ tone: 'pending', remaining: 'Ожидает' }))).toEqual({ kind: 'plain', remaining: 'Ожидает' });
    expect(seatTileLead(seat({ tone: 'service', remaining: 'Закрыт' }))).toEqual({ kind: 'plain', remaining: 'Закрыт' });
  });
});
