import { describe, expect, it } from 'bun:test';
import { toReportQuery } from './reportRange';

describe('toReportQuery', () => {
  it('uses a half-open range that includes the full selected end date', () => {
    const query = toReportQuery({ from: '2026-07-01', to: '2026-07-03' });
    expect(new Date(query.toUtc).getTime() - new Date(query.fromUtc).getTime()).toBe(3 * 86_400_000);
    expect(query.limit).toBe(200);
  });
});
