import { describe, expect, it } from 'bun:test';
import { toReportQuery } from './reportRange';

describe('toReportQuery', () => {
  it('sends local calendar dates for server-side branch timezone resolution', () => {
    const query = toReportQuery({ from: '2026-07-01', to: '2026-07-03' });
    expect(query.fromDate).toBe('2026-07-01');
    expect(query.toDate).toBe('2026-07-03');
  });
});
