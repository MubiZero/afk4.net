import type { MessageKey } from '@afk4/i18n';
import type { ShiftRevenueDto } from '../operatorApiClients';
import { escapeHtml, formatMoney, formatTime, readMoney } from '../operatorHelpers';

type Money = { currencyCode: string; minorUnits: number };
type TFunc = (key: MessageKey) => string;

// Данные печатной/экранной формы отчёта по смене (X = промежуточный снимок, Z = итог закрытия).
export interface ShiftReportData {
  openedAtUtc: string;
  closedAtUtc: string | null;
  earned: { time: Money; goods: Money; total: Money };
  inflow: { cash: Money; nonCash: Money; walletTopUps: Money };
  cash: { starting: Money; expected: Money; counted: Money | null; difference: Money | null };
}

// X = снимок текущей выручки/сверки как есть. Z = тот же снимок, но counted/difference/closedAt
// берём из ответа close (выручки в ответе close нет — она остаётся из снимка revenue).
export function buildShiftReportData(revenue: ShiftRevenueDto, closeResult?: Record<string, unknown> | null): ShiftReportData {
  const counted = closeResult ? readMoney(closeResult, 'countedCash') : revenue.cash.counted;
  const difference = closeResult ? readMoney(closeResult, 'difference') : revenue.cash.difference;
  const closedAtUtc = closeResult
    ? (typeof closeResult.closedAtUtc === 'string' ? closeResult.closedAtUtc : null)
    : revenue.closedAtUtc;
  return {
    openedAtUtc: revenue.openedAtUtc,
    closedAtUtc,
    earned: revenue.earned,
    inflow: revenue.inflow,
    cash: { starting: revenue.cash.starting, expected: revenue.cash.expected, counted, difference }
  };
}

// Моноширинный текст отчёта для печати (паттерн buildPosReceiptText).
export function buildShiftReportText(data: ShiftReportData, variant: 'x' | 'z', currencyCode: string, t: TFunc): string {
  const title = variant === 'x' ? t('op.cash.report.xTitle') : t('op.cash.report.zTitle');
  const row = (label: MessageKey, value: Money | null) => `${t(label)}: ${formatMoney(value, currencyCode)}`;
  const lines: string[] = [
    t('op.cash.report.printHeader'),
    title,
    `${t('op.cash.report.opened')}: ${formatTime(data.openedAtUtc)}`,
  ];
  if (data.closedAtUtc) lines.push(`${t('op.cash.report.closed')}: ${formatTime(data.closedAtUtc)}`);
  lines.push(
    '',
    t('op.cash.report.revenueSection'),
    row('op.shifts.earned', data.earned.total),
    row('op.shifts.time', data.earned.time),
    row('op.shifts.goods', data.earned.goods),
    row('op.shifts.cash', data.inflow.cash),
    row('op.shifts.nonCash', data.inflow.nonCash),
    row('op.shifts.walletTopUps', data.inflow.walletTopUps),
    '',
    t('op.cash.report.reconcileSection'),
    row('op.cash.shift.starting', data.cash.starting),
    row('op.cash.shift.expected', data.cash.expected),
    `${t('op.cash.shift.counted')}: ${data.cash.counted === null ? t('op.cash.shift.notClosed') : formatMoney(data.cash.counted, currencyCode)}`,
    `${t('op.cash.shift.difference')}: ${data.cash.difference === null ? t('op.cash.shift.notClosed') : formatMoney(data.cash.difference, currencyCode)}`
  );
  return lines.join('\n');
}

// Печать через новое окно (паттерн printSelectedReceipt). false, если окно не открылось (тесты/блокировщик).
export function printShiftReport(title: string, text: string): boolean {
  const printWindow = window.open('', '_blank', 'width=360,height=640');
  if (printWindow === null) return false;
  printWindow.document.write(`<title>${escapeHtml(title)}</title><pre style="font: 13px/1.45 monospace; white-space: pre-wrap;">${escapeHtml(text)}</pre>`);
  printWindow.document.close();
  printWindow.focus();
  printWindow.print();
  return true;
}
