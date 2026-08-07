import { describe, expect, it } from 'bun:test';
import type { AnalyticsMonth, AnalyticsOverview } from '@/api/types';
import { isEmpty, toRevenueSeries, totalRevenue } from './analyticsModel';

function month(overrides: Partial<AnalyticsMonth> = {}): AnalyticsMonth {
  return {
    year: 2026,
    month: 1,
    recurringMinorUnits: 0,
    oneOffMinorUnits: 0,
    joined: 0,
    left: 0,
    payingAtMonthEnd: 0,
    ...overrides
  };
}

function overview(months: AnalyticsMonth[]): AnalyticsOverview {
  return {
    generatedAtUtc: '2026-08-07T00:00:00Z',
    currencyCode: 'TJS',
    months,
    currentMrrMinorUnits: 0,
    currentPayingClubs: 0,
    averageRevenuePerClubMinorUnits: 0,
    outstandingMinorUnits: 0
  };
}

describe('toRevenueSeries', () => {
  it('переводит минорные единицы в мажорные и берёт подписи из переданной функции', () => {
    const series = toRevenueSeries(
      [month({ month: 3, recurringMinorUnits: 150000, oneOffMinorUnits: 2500 })],
      (m) => `label-${m}`
    );
    expect(series).toEqual([{ label: 'label-3', recurring: 1500, oneOff: 25 }]);
  });
});

describe('totalRevenue', () => {
  it('складывает подписки и разовые начисления по всем месяцам', () => {
    const total = totalRevenue([
      month({ recurringMinorUnits: 100000, oneOffMinorUnits: 5000 }),
      month({ recurringMinorUnits: 200000, oneOffMinorUnits: 0 })
    ]);
    expect(total).toBe(305000);
  });
});

describe('isEmpty', () => {
  it('истинно, когда во всех месяцах выручка и движение по нулям', () => {
    expect(isEmpty(overview([month(), month()]))).toBe(true);
  });

  it('ложно, если хотя бы в одном месяце есть платящий клуб', () => {
    expect(isEmpty(overview([month(), month({ payingAtMonthEnd: 3 })]))).toBe(false);
  });

  it('ложно, если хотя бы в одном месяце есть ненулевая выручка', () => {
    expect(isEmpty(overview([month({ recurringMinorUnits: 1 }), month()]))).toBe(false);
  });
});
