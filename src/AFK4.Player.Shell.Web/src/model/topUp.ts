import type { MessageKey } from '@afk4/i18n';
import { majorToMinor } from '@afk4/money';

/** Суммы, которые игрок кладёт чаще всего, — в основных единицах: 20, 50, 100 и 200. */
export const TOP_UP_PRESETS_MAJOR = [20, 50, 100, 200];

/** Как часто спрашивать банк и сколько ждать: те же 3 секунды и 5 минут, что в приложении. */
export const BANK_POLL_MS = 3_000;
export const BANK_WAIT_MS = 5 * 60_000;

/** «35» или «35,5» → 3550 младших единиц; кривая или нулевая сумма — null. */
export function parseMajorAmount(text: string): number | null {
  const normalized = text.trim().replace(/\s/g, '').replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) return null;
  const minor = majorToMinor(Number(normalized));
  return minor > 0 ? minor : null;
}

/** Ответ банка на «оплатили?» — paid, failed или pending (так же отвечает сервер приложению). */
export type BankPayment = 'paid' | 'failed' | 'pending';

export type TopUpOutcome = { kind: 'paid'; minorUnits: number } | { kind: 'problem'; key: MessageKey };

/**
 * Чем кончилось ожидание. Оплачено — только по слову сервера, после зачисления: «QR отсканировали»
 * ничего не значит. Не дождались за пять минут — не «ошибка»: деньги зачислит вебхук банка.
 */
export function outcomeOf(payment: BankPayment, waitedMs: number, amountMinor: number): TopUpOutcome | null {
  if (payment === 'paid') return { kind: 'paid', minorUnits: amountMinor };
  if (payment === 'failed') return { kind: 'problem', key: 'playerShell.topUp.failed' };
  return waitedMs >= BANK_WAIT_MS ? { kind: 'problem', key: 'playerShell.topUp.slow' } : null;
}
