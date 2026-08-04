import { PlatformApiError } from './platformApi';
import type { MessageKey } from '@/i18n/messages';

type Translate = (key: MessageKey, values?: Record<string, string | number>) => string;

/**
 * Человеческий текст ошибки для показа пользователю.
 *
 * Транспорт носит технические англоязычные сообщения («Sign-in failed.», «Platform API call
 * failed.») — они нужны в логах, но в интерфейс попадать не должны: русский экран с английской
 * строкой читается как поломка, а сам текст ничего не объясняет тому, кто его читает.
 *
 * Поэтому текст выбирается по КОДУ ответа, а вызывающий экран может уточнить отдельные коды
 * своей формулировкой (`overrides`) — например, 404 «код приглашения не найден».
 */
export function describeApiError(
  cause: unknown,
  t: Translate,
  overrides: Partial<Record<number, MessageKey>> = {}
): string {
  if (cause instanceof PlatformApiError) {
    const override = overrides[cause.status];
    if (override !== undefined) return t(override);
    if (cause.status === 401 || cause.status === 403) return t('state.error.forbidden');
    // Статус 0 транспорт ставит, когда ответа не было вовсе: сеть, выключенный сервер, CORS.
    if (cause.status === 0) return t('state.error.network');
    return t('state.error.server');
  }
  // Сетевой сбой до ответа — fetch бросает TypeError без статуса.
  if (cause instanceof TypeError) return t('state.error.network');
  return t('state.error.server');
}
