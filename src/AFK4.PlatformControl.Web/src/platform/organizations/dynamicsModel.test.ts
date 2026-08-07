import { describe, expect, test } from 'bun:test';
import { toDynamicsSeries, countAliveDays } from './dynamicsModel';

const day = (date: string, sessions: number, minorUnits: number, agentAlive: boolean | null) => ({
  date,
  sessionCount: sessions,
  revenue: { currencyCode: 'TJS', minorUnits },
  shiftOpenedCount: 1,
  agentAlive
});

describe('toDynamicsSeries', () => {
  test('переводит минорные единицы в мажорные для графика', () => {
    const series = toDynamicsSeries([day('2026-08-01', 4, 12_345, true)]);
    expect(series[0].revenue).toBe(123.45);
    expect(series[0].sessions).toBe(4);
  });

  test('сохраняет порядок дней', () => {
    const series = toDynamicsSeries([day('2026-08-01', 1, 100, true), day('2026-08-02', 2, 200, true)]);
    expect(series.map(point => point.date)).toEqual(['2026-08-01', '2026-08-02']);
  });
});

describe('countAliveDays', () => {
  test('считает только дни с agentAlive === true, не смешивая с «не выходил на связь» и «нет данных»', () => {
    const count = countAliveDays([
      day('2026-08-01', 0, 0, false),
      day('2026-08-02', 0, 0, null),
      day('2026-08-03', 0, 0, true)
    ]);
    expect(count).toBe(1);
  });
});
