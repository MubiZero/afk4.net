import { it, expect } from 'bun:test';
import { resolvePlayerRoute, routePath } from './routing';

it('maps the root path to the dashboard tab', () => {
  expect(resolvePlayerRoute('/').kind).toBe('dashboard');
});

it('maps /history to the history tab', () => {
  expect(resolvePlayerRoute('/history').kind).toBe('history');
});

it('parses a receipt route with its session id', () => {
  const route = resolvePlayerRoute('/history/abc-123/receipt');
  expect(route).toEqual({ kind: 'receipt', sessionId: 'abc-123' });
});

it('falls back to the dashboard for unknown paths', () => {
  expect(resolvePlayerRoute('/nonsense').kind).toBe('dashboard');
});

it('round-trips a tab through routePath', () => {
  expect(routePath({ kind: 'reservations' })).toBe('/reservations');
});
