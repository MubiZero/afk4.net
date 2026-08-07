import { minorToMajor } from '@/lib/money';
import type { AnalyticsMonth, AnalyticsOverview } from '@/api/types';

export interface RevenuePoint {
  label: string;
  recurring: number;
  oneOff: number;
}

// Подписи месяцев собираются на клиенте из года и номера месяца: сервер отдаёт числа,
// потому что название месяца зависит от языка пользователя, которого сервер не знает.
// Минор→мажор идёт через общий `minorToMajor`, ту же функцию, что использует AnalyticsTab
// для сводных плиток — самописное деление здесь разошлось бы с ними молча при любом
// изменении точности в @afk4/money.
export function toRevenueSeries(
  months: readonly AnalyticsMonth[],
  monthLabel: (month: number) => string
): RevenuePoint[] {
  return months.map(month => ({
    label: monthLabel(month.month),
    recurring: minorToMajor(month.recurringMinorUnits),
    oneOff: minorToMajor(month.oneOffMinorUnits)
  }));
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
