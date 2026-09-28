import { PlatformApiError } from './platformApi';
import { createIdempotencyKey } from './operatorHelpers';

/** Дольше этого повтор — уже новое намерение: ПК снова завис, и перезагрузить его надо заново. */
const RETRY_WINDOW_MS = 2 * 60_000;

/**
 * Неизвестно, дошла ли просьба до сервера. Одно правило на всю Панель: раньше бронь, бар и
 * касса отвечали на это по-разному, и один и тот же таймаут где-то держал ключ, а где-то нет.
 *
 * Неизвестно — обрыв сети, 5xx (прокси мог ответить после записи), 408 и 425 (сервер
 * не дождался или отложил), 429 (очередь). Остальные 4xx — сервер прочитал и отказал; по
 * ключу он хранит только успешные ответы, так что повторять с тем же ключом незачем.
 */
export function isOutcomeUnknown(error: unknown): boolean {
  if (!(error instanceof PlatformApiError)) return true;
  return error.status >= 500 || error.status === 408 || error.status === 425 || error.status === 429;
}

/**
 * Ключи нажатий, исход которых неизвестен.
 *
 * Связь оборвалась посреди запроса — непонятно, дошла ли команда. Кассир жмёт «Пополнить»
 * ещё раз, и это нажатие обязано уйти с тем же ключом: тогда сервер узнает повтор и вернёт
 * уже записанный ответ, а не зачислит деньги второй раз. Ответ пришёл — успех или отказ —
 * ключ забыт: следующее нажатие честно новое.
 *
 * Намерение — это сама просьба: тот же игрок, та же сумма, та же причина. Сменил сумму после
 * обрыва — это другое нажатие, и старый ключ ему не достаётся (иначе сервер ответил бы
 * конфликтом ключа).
 *
 * Ключ живёт в памяти и пропадает с перезагрузкой Панели. Там, где у сервера есть что
 * прочитать — оплачен ли чек, начата ли бронь, открыта ли смена, — экран после сбоя ещё и
 * сверяется с ним: это второй слой, а не второй способ держать ключ.
 */
export class UnsettledKeys {
  private readonly keys = new Map<string, { key: string; issuedAtMs: number }>();

  constructor(private readonly now: () => number = Date.now) {}

  /**
   * `firstAttemptAt` — момент первого нажатия. Просьбе, в которой есть «с этого момента» (цена
   * тарифа), повтор обязан прислать ту же дату, иначе сервер сочтёт её другой просьбой.
   */
  async send<T>(
    operationName: string,
    intent: unknown,
    request: (idempotencyKey: string, firstAttemptAt: Date) => Promise<T>
  ): Promise<T> {
    const slot = slotOf(operationName, intent);
    const attempt = this.attemptFor(slot, operationName);
    try {
      const result = await request(attempt.key, new Date(attempt.issuedAtMs));
      this.keys.delete(slot);
      return result;
    } catch (failure) {
      if (!isOutcomeUnknown(failure)) {
        this.keys.delete(slot);
      }
      throw failure;
    }
  }

  /** Оператор сам решил: прошлой попытки не было, начинаем заново («Новая попытка»). */
  forget(operationName: string, intent: unknown): void {
    this.keys.delete(slotOf(operationName, intent));
  }

  private attemptFor(slot: string, operationName: string): { key: string; issuedAtMs: number } {
    const unsettled = this.keys.get(slot);
    if (unsettled !== undefined && this.now() - unsettled.issuedAtMs < RETRY_WINDOW_MS) {
      return unsettled;
    }
    const attempt = { key: createIdempotencyKey(operationName), issuedAtMs: this.now() };
    this.keys.set(slot, attempt);
    return attempt;
  }
}

function slotOf(operationName: string, intent: unknown): string {
  return `${operationName}|${JSON.stringify(intent)}`;
}

/**
 * Одни ключи на всю Панель: кассир после обрыва может закрыть окно и открыть его снова, и
 * повтор всё равно должен узнаться. Намерения разных экранов не пересекаются — в них имя
 * операции и сама просьба.
 */
export const retryKeys = new UnsettledKeys();
