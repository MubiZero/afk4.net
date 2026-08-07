import type { DebtRow } from '@/api/types';
import type { MessageKey } from '@/i18n/messages';

const STAGE_LABEL_KEY: readonly MessageKey[] = [
  'platform.debt.stage.none',
  'platform.debt.stage.first',
  'platform.debt.stage.second',
  'platform.debt.stage.third',
  'platform.debt.stage.final'
];

export function dunningStageLabelKey(stage: number): MessageKey {
  return STAGE_LABEL_KEY[stage] ?? STAGE_LABEL_KEY[0];
}

/**
 * Раздел «Задолженность» открывается ЭТИМ порядком: клуб, который тянет долг дольше всех,
 * ждал решения дольше всех, и это первый кандидат на разговор.
 */
export function sortDebtRows(rows: DebtRow[]): DebtRow[] {
  return [...rows].sort((left, right) => {
    const byDays = right.daysOverdue - left.daysOverdue;
    if (byDays !== 0) return byDays;
    return left.organizationName.localeCompare(right.organizationName);
  });
}

/** Итог по валютам: клубы, которые уже погасили долг (но остались отключены), в сумму не входят —
 * им ничего не должны, их напоминание другого рода. */
export function debtTotals(rows: DebtRow[]): { currencyCode: string; amountMinorUnits: number }[] {
  const byCurrency = new Map<string, number>();
  for (const row of rows) {
    if (row.outstandingMinorUnits <= 0) continue;
    byCurrency.set(row.currencyCode, (byCurrency.get(row.currencyCode) ?? 0) + row.outstandingMinorUnits);
  }
  return [...byCurrency.entries()]
    .map(([currencyCode, amountMinorUnits]) => ({ currencyCode, amountMinorUnits }))
    .sort((left, right) => left.currencyCode.localeCompare(right.currencyCode));
}
