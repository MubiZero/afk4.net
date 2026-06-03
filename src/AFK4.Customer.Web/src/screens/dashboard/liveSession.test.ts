import { it, expect } from 'bun:test';
import { elapsedSeconds, formatClock, projectRemainingSeconds } from './liveSession';

it('formats seconds as HH:MM:SS', () => {
  expect(formatClock(0)).toBe('00:00:00');
  expect(formatClock(6138)).toBe('01:42:18');
});

it('computes elapsed seconds from the start time', () => {
  const start = '2026-06-03T20:00:00Z';
  const now = new Date('2026-06-03T21:42:18Z');
  expect(elapsedSeconds(start, now)).toBe(6138);
});

it('counts a fixed session down and clamps at zero', () => {
  const fetchedAt = new Date('2026-06-03T20:00:00Z');
  const now = new Date('2026-06-03T20:00:30Z');
  expect(projectRemainingSeconds(100, fetchedAt, now)).toBe(70);
  expect(projectRemainingSeconds(10, fetchedAt, now)).toBe(0);
});
