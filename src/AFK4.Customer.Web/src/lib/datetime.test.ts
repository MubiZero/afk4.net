import { it, expect } from 'bun:test';
import { formatDateTime, formatDuration } from './datetime';

it('formats a duration between two instants as "Hч Mм"', () => {
  expect(formatDuration('2026-06-03T10:00:00Z', '2026-06-03T12:30:00Z')).toBe('2ч 30м');
});

it('formats a sub-hour duration as just minutes', () => {
  expect(formatDuration('2026-06-03T10:00:00Z', '2026-06-03T10:45:00Z')).toBe('45м');
});

it('returns an empty string for an invalid date', () => {
  expect(formatDateTime('not-a-date')).toBe('');
});

it('renders a valid instant containing a time separator', () => {
  expect(formatDateTime('2026-06-03T20:05:00Z')).toContain(':');
});
