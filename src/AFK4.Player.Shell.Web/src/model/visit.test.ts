import { describe, expect, it } from 'bun:test';
import { endedSessionId, playedMinutes } from './visit';

describe('визит кончился', () => {
  const me = 'player-me';
  const mine = (screen: 'session' | 'ending') => ({ screen, sessionId: 's-1', ownerPlayerAccountId: me });

  it('сессия вошедшего закрылась — по таймеру или у стойки — итог по ней', () => {
    expect(endedSessionId(mine('session'), 'chooseTime', me)).toBe('s-1');
    expect(endedSessionId(mine('ending'), 'chooseTime', me)).toBe('s-1');
  });

  it('вышел из аккаунта — итог уже не его', () => {
    expect(endedSessionId(mine('session'), 'idle', null)).toBeNull();
  });

  it('связь пропала — это не конец визита', () => {
    expect(endedSessionId(mine('session'), 'grace', me)).toBeNull();
  });

  it('кончилась чужая сессия — стойка посадила сюда другого, пока этот был вошедшим, — итога нет', () => {
    expect(endedSessionId({ screen: 'session', sessionId: 's-1', ownerPlayerAccountId: 'player-other' }, 'chooseTime', me)).toBeNull();
    expect(endedSessionId({ screen: 'session', sessionId: 's-1', ownerPlayerAccountId: null }, 'chooseTime', me)).toBeNull();
  });

  const visit = { startedAtUtc: null, endedAtMs: Date.parse('2026-09-25T12:00:00Z') };

  it('сыграно — по чеку, меньше минуты всё равно минута', () => {
    expect(playedMinutes({ startedAtUtc: '2026-09-25T10:00:00Z', endedAtUtc: '2026-09-25T11:35:40Z' }, visit)).toBe(95);
    expect(playedMinutes({ startedAtUtc: '2026-09-25T10:00:00Z', endedAtUtc: '2026-09-25T10:00:20Z' }, visit)).toBe(1);
    expect(playedMinutes({ startedAtUtc: '2026-09-25T10:00:00Z', endedAtUtc: null }, visit)).toBeNull();
  });

  it('чека нет — сыграно по началу сессии и её концу на ПК, а не по оплаченным минутам', () => {
    expect(playedMinutes(null, { startedAtUtc: '2026-09-25T11:38:00Z', endedAtMs: visit.endedAtMs })).toBe(22);
    expect(playedMinutes(null, visit)).toBeNull();
  });
});
