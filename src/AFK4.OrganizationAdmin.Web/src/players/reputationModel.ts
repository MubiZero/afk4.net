import type { MessageKey } from '@afk4/i18n';
import { PlatformApiError } from '../platformApi';
import { localPhoneDigits } from '../phoneFormat';
import type { PlayerReputationDto } from '../operatorApiClients';

export type ReputationTone = 'clean' | 'watch' | 'banned';

/**
 * Точный номер для спроса у сети, или null — если номера как такового нет.
 *
 * Сеть отвечает только на полный номер: по огрызку она бы работала поиском «а есть ли такой
 * человек», и маршрут это отдельно запрещает. Поэтому неполный номер даже не отправляем.
 */
export function reputationLookupPhone(raw: string): string | null {
  const local = localPhoneDigits(raw);
  return local.length === 9 ? `+992${local}` : null;
}

/**
 * Тон карточки. Ноль визитов — это «сеть его не знает», а не «подозрительный»: у только что
 * зарегистрировавшегося человека и у незнакомого сети номера ответ одинаковый по построению.
 */
export function reputationTone(reputation: PlayerReputationDto): ReputationTone {
  if (reputation.networkBanned) return 'banned';
  return reputation.networkNoShows > 0 ? 'watch' : 'clean';
}

/**
 * Причина отказа маршрута словами оператора, или null — тогда показываем общую проекцию ошибки
 * (projectOperatorError), а не выдумываем свой текст поверх настоящего.
 */
export function reputationErrorKey(error: unknown): MessageKey | null {
  if (!(error instanceof PlatformApiError)) return null;
  if (error.status === 400) return 'op.reputation.invalidPhone';
  if (error.status === 429) return 'op.reputation.tooManyLookups';
  if (error.status === 404) return 'op.reputation.unknown';
  return null;
}
