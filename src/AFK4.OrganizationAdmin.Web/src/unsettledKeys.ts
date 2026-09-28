import { PlatformApiError } from './platformApi';
import { createIdempotencyKey } from './operatorHelpers';

/** Дольше этого повтор — уже новое намерение: ПК снова завис, и перезагрузить его надо заново. */
const RETRY_WINDOW_MS = 2 * 60_000;

/**
 * Ключи нажатий, на которые сервер так и не ответил.
 *
 * Связь оборвалась посреди запроса — неизвестно, дошла ли команда. Оператор жмёт «Перезагрузить»
 * ещё раз, и это нажатие обязано уйти с тем же ключом: тогда сервер узнает повтор и вернёт уже
 * записанную команду, а не пошлёт вторую перезагрузку в загружающийся Windows. Ответ пришёл —
 * успех или отказ, неважно, — и ключ забыт: следующее нажатие честно новое.
 */
export class UnsettledKeys {
  private readonly keys = new Map<string, { key: string; issuedAtMs: number }>();

  constructor(private readonly now: () => number = Date.now) {}

  async send<T>(slot: string, operationName: string, request: (idempotencyKey: string) => Promise<T>): Promise<T> {
    const idempotencyKey = this.keyFor(slot, operationName);
    try {
      const result = await request(idempotencyKey);
      this.keys.delete(slot);
      return result;
    } catch (failure) {
      // Сервер ответил отказом — команда точно не записана с этим ключом или записана и отвергнута.
      // Держать ключ дальше значило бы подсунуть следующему нажатию чужой ответ.
      if (failure instanceof PlatformApiError) {
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
