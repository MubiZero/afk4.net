import type { AnalyticsMonth, AnalyticsOverview } from '@/api/types';

export interface RevenuePoint {
  label: string;
  recurring: number;
  oneOff: number;
}

// Подписи месяцев собираются на клиенте из года и номера месяца: сервер отдаёт числа,
// потому что название месяца зависит от языка пользователя, которого сервер не знает.
export function toRevenueSeries(
  months: readonly AnalyticsMonth[],
  monthLabel: (month: number) => string
): RevenuePoint[] {
  return months.map(month => ({
    label: monthLabel(month.month),
    recurring: month.recurringMinorUnits / 100,
    oneOff: month.oneOffMinorUnits / 100
  }));
}

export function totalRevenue(months: readonly AnalyticsMonth[]): number {
  return months.reduce((sum, month) => sum + month.recurringMinorUnits + month.oneOffMinorUnits, 0);
}

// «Данных нет» — это когда во всех месяцах и выручка, и движение по нулям. Отличать от ошибки
// загрузки обязательно: пустой график и несостоявшийся запрос выглядят одинаково, но означают
// противоположное.
export function isEmpty(overview: AnalyticsOverview): boolean {
  return overview.months.every(month =>
    month.recurringMinorUnits === 0
    && month.oneOffMinorUnits === 0
    && month.payingAtMonthEnd === 0);
}
