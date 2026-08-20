import { describe, expect, it } from 'bun:test';
import { messages } from '@afk4/i18n';
import { buildShiftReportData, buildShiftReportText } from './shiftReport';
import type { ShiftRevenueDto } from '../operatorApiClients';

const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });
// мини-t без провайдера: бьём прямо в ru-словарь
const t = (key: string) => (messages.ru as Record<string, string>)[key] ?? key;

function openRevenue(): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o', branchId: 'b1',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: m(80000), goods: m(41000), noShow: m(2000), total: m(123000) },
    inflow: { cash: m(90000), nonCash: m(33000), walletTopUps: m(15000), directTotal: m(123000) },
    cash: { starting: m(100000), expected: m(190000), counted: null, difference: null },
    openedAtUtc: '2026-06-24T08:00:00Z', closedAtUtc: null
  };
}

describe('buildShiftReportData', () => {
  it('X: берёт снимок открытой смены как есть (counted/difference = null)', () => {
    const data = buildShiftReportData(openRevenue());
    expect(data.cash.counted).toBeNull();
    expect(data.cash.difference).toBeNull();
    expect(data.earned.total).toEqual(m(123000));
    expect(data.closedAtUtc).toBeNull();
  });

  it('Z: накладывает counted/difference/closedAt из результата закрытия', () => {
    const data = buildShiftReportData(openRevenue(), {
      countedCash: m(185000), difference: m(-5000), closedAtUtc: '2026-06-24T18:00:00Z'
    });
    expect(data.cash.counted).toEqual(m(185000));
    expect(data.cash.difference).toEqual(m(-5000));
    expect(data.cash.starting).toEqual(m(100000)); // из снимка
    expect(data.closedAtUtc).toBe('2026-06-24T18:00:00Z');
  });
});

describe('buildShiftReportText', () => {
  it('X-текст содержит заголовок, выручку и сверку', () => {
    const text = buildShiftReportText(buildShiftReportData(openRevenue()), 'x', 'TJS', t);
    expect(text).toContain('X-отчёт');
    expect(text).toContain('Выручка смены');
    expect(text).toContain('Сверка кассы');
    expect(text).toContain('230 с.');
  });

  it('удержания за неявку — отдельная строка выручки в печатном отчёте', () => {
    const text = buildShiftReportText(buildShiftReportData(openRevenue()), 'x', 'TJS', t);
    expect(text).toContain('Неявки: 20 с.');
  });

  it('Z-текст помечен как Z и показывает расхождение', () => {
    const data = buildShiftReportData(openRevenue(), { countedCash: m(185000), difference: m(-5000), closedAtUtc: '2026-06-24T18:00:00Z' });
    const text = buildShiftReportText(data, 'z', 'TJS', t);
    expect(text).toContain('Z-отчёт');
    expect(text).toContain('-50 с.');
  });
});
