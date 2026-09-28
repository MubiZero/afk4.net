import { PlatformApiError } from './platformApi';
import { createIdempotencyKey } from './operatorHelpers';

/** Дольше этого повтор — уже новое намерение: ПК снова завис, и перезагрузить его надо заново. */
const RETRY_WINDOW_MS = 2 * 60_000;

/**
 * Ключи нажатий, исход которых неизвестен.
 *
 * Связь оборвалась посреди запроса — непонятно, дошла ли команда. Кассир жмёт «Пополнить»
 * ещё раз, и это нажатие обязано уйти с тем же ключом: тогда сервер узнает повтор и вернёт
 * уже записанный ответ, а не зачислит деньги второй раз. Ключ живёт, пока исход неизвестен:
 * обрыв сети или 5xx от прокси (сервер мог успеть записать). Любой ответ 4xx — сервер
 * отказал и по ключу ничего не хранит — ключ забыт, как и после успеха: следующее нажатие
 * честно новое.
 *
 * Намерение — это сама просьба: тот же игрок, та же сумма, та же причина. Сменил сумму после
 * обрыва — это другое нажатие, и старый ключ ему не достаётся (иначе сервер ответил бы
 * конфликтом ключа).
 */
export class UnsettledKeys {
  private readonly keys = new Map<string, { key: string; issuedAtMs: number }>();

  constructor(private readonly now: () => number = Date.now) {}

  async send<T>(operationName: string, intent: unknown, request: (idempotencyKey: string) => Promise<T>): Promise<T> {
    const slot = `${operationName}|${JSON.stringify(intent)}`;
    const idempotencyKey = this.keyFor(slot, operationName);
    try {
      const result = await request(idempotencyKey);
      this.keys.delete(slot);
      return result;
    } catch (failure) {
      if (failure instanceof PlatformApiError && failure.status < 500) {
        this.keys.delete(slot);
      }
      throw failure;
    }
  }

  private keyFor(slot: string, operationName: string): string {
    const unsettled = this.keys.get(slot);
    if (unsettled !== undefined && this.now() - unsettled.issuedAtMs < RETRY_WINDOW_MS) {
      return unsettled.key;
    }
    const key = createIdempotencyKey(operationName);
    this.keys.set(slot, { key, issuedAtMs: this.now() });
    return key;
  }
}

/**
 * Одни ключи на всю Панель: кассир после обрыва может закрыть окно и открыть его снова, и
 * повтор всё равно должен узнаться. Намерения разных экранов не пересекаются — в них имя
 * операции и сама просьба.
 */
export const retryKeys = new UnsettledKeys();
