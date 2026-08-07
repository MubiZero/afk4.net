import { expect, it } from 'bun:test';
import { dunningStageLabelKey, sortDebtRows, debtTotals } from './debtModel';
import type { DebtRow } from '@/api/types';

function row(overrides: Partial<DebtRow> = {}): DebtRow {
  return {
    organizationId: 'o1',
    organizationName: 'Арена',
    organizationSlug: 'arena',
    organizationStatus: 'active',
    subscriptionStatus: 'past_due',
    outstandingMinorUnits: 290000,
    currencyCode: 'TJS',
    oldestOverdueInvoiceNumber: 1,
    oldestOverdueInvoiceId: 'i1',
    daysOverdue: 10,
    dunningStage: 3,
    graceUntilUtc: null,
    settledButSuspended: false,
    ...overrides
  };
}

it('ставит самый старый долг первым', () => {
  const rows = sortDebtRows([row({ organizationName: 'Свежий', daysOverdue: 2 }), row({ organizationName: 'Старый', daysOverdue: 30 })]);
  expect(rows.map(r => r.organizationName)).toEqual(['Старый', 'Свежий']);
});

it('складывает долг по валютам', () => {
  const totals = debtTotals([row({ outstandingMinorUnits: 290000 }), row({ outstandingMinorUnits: 100000 })]);
  expect(totals).toEqual([{ currencyCode: 'TJS', amountMinorUnits: 390000 }]);
});

it('не считает в итог клуб без долга, оставшийся отключённым', () => {
  const totals = debtTotals([row({ outstandingMinorUnits: 0, settledButSuspended: true })]);
  expect(totals).toEqual([]);
});

it('переводит ступень напоминаний в ключ строки', () => {
  expect(dunningStageLabelKey(0)).toBe('platform.debt.stage.none');
  expect(dunningStageLabelKey(4)).toBe('platform.debt.stage.final');
});
