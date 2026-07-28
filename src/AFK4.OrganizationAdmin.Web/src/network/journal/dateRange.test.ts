import { describe, it, expect } from 'bun:test';
import { presetRange } from './dateRange';

describe('presetRange', () => {
  it('today spans the full UTC day', () => {
    const r = presetRange('today', new Date('2026-07-20T12:00:00Z'));
    expect(r.fromUtc).toBe('2026-07-20T00:00:00.000Z');
    expect(r.toUtc).toBe('2026-07-20T23:59:59.000Z');
  });
  it('7d goes back six days', () => {
    const r = presetRange('7d', new Date('2026-07-20T12:00:00Z'));
    expect(r.fromUtc).toBe('2026-07-14T00:00:00.000Z');
  });
});
