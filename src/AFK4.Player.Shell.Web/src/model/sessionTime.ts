import type { PlayerShellStateDto } from '@afk4/contracts';

/**
 * Продление уже подтверждено сервером, а агент принесёт новый конец только со следующим
 * сердцебиением (секунд через пятнадцать): до тех пор таймер считал бы до старого. Берём более
 * поздний из двух концов — и только для той же сессии.
 */
export function withConfirmedEnd(
  state: PlayerShellStateDto,
  confirmed: { sessionId: string; endsAtUtc: string } | null
): PlayerShellStateDto {
  if (!confirmed || confirmed.sessionId !== state.sessionId) return state;
  const current = state.sessionEndsAtUtc ?? null;
  if (current && Date.parse(current) >= Date.parse(confirmed.endsAtUtc)) return state;
  return { ...state, sessionEndsAtUtc: confirmed.endsAtUtc };
}

/**
 * До какого момента идёт сессия на экране. Отсчёт — от конца сессии, а не от аренды: аренду сервер
 * подписывает на 15 минут и продлевает, пока сессия идёт, и отсчёт по ней прыгал бы между 5 и 15
 * минутами. Без связи сессия живёт не дольше подписанной аренды — тогда ближайшее из двух.
 * null — конца нет (открытый счёт на связи): экран показывает, сколько уже идёт.
 */
export function sessionUntilUtc(state: PlayerShellStateDto): string | null {
  const end = state.sessionEndsAtUtc ?? null;
  const lease = state.leaseExpiresAtUtc ?? null;
  const online = state.isOnline && !state.isGraceMode;
  if (online) return end;
  if (end && lease) return Date.parse(end) < Date.parse(lease) ? end : lease;
  return end ?? lease;
}
