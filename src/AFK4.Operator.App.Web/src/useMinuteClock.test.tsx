import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, jest } from 'bun:test';
import { useMinuteClock } from './useMinuteClock';

afterEach(() => {
  jest.useRealTimers();
});

describe('useMinuteClock', () => {
  it('updates on the next minute boundary and after visibility resume', () => {
    jest.useFakeTimers();
    let now = new Date('2026-07-15T11:02:45');
    const nowProvider = () => now;

    const { result } = renderHook(() => useMinuteClock('ru-RU', nowProvider));
    expect(result.current).toBe('11:02');

    now = new Date('2026-07-15T11:03:00');
    act(() => jest.advanceTimersByTime(15_000));
    expect(result.current).toBe('11:03');

    now = new Date('2026-07-15T11:17:10');
    act(() => document.dispatchEvent(new Event('visibilitychange')));
    expect(result.current).toBe('11:17');
  });
});
