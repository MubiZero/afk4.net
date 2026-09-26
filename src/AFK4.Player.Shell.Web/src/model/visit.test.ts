import { describe, expect, it } from 'bun:test';
import { endedSessionId, playedMinutes } from './visit';

describe('визит кончился', () => {
  it('сессия вошедшего закрылась — по таймеру или у стойки — итог по ней', () => {
    expect(endedSessionId({ screen: 'session', sessionId: 's-1' }, 'chooseTime', true)).toBe('s-1');
    expect(endedSessionId({ screen: 'ending', sessionId: 's-1' }, 'chooseTime', true)).toBe('s-1');
  });

  it('вышел из аккаунта — итог уже не его', () => {
    expect(endedSessionId({ screen: 'session', sessionId: 's-1' }, 'idle', false)).toBeNull();
  });

  it('связь пропала — это не конец визита', () => {
    expect(endedSessionId({ screen: 'session', sessionId: 's-1' }, 'grace', true)).toBeNull();
  });

  it('сыграно — по чеку, меньше минуты всё равно минута', () => {
    expect(playedMinutes({ startedAtUtc: '2026-09-25T10:00:00Z', endedAtUtc: '2026-09-25T11:35:40Z' })).toBe(95);
    expect(playedMinutes({ startedAtUtc: '2026-09-25T10:00:00Z', endedAtUtc: '2026-09-25T10:00:20Z' })).toBe(1);
    expect(playedMinutes({ startedAtUtc: '2026-09-25T10:00:00Z', endedAtUtc: null })).toBeNull();
  });
});
