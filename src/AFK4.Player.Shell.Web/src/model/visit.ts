import type { PlayerSelfEndSessionResponse, PlayerVisitReceiptDto } from '@afk4/contracts';
import { isSessionScreen, type ShellScreen } from './screen';

/** Визит, который только что кончился: по нему итог, чек и оценка. */
export interface EndedVisit {
  sessionId: string;
  /** Ответ на «Встать раньше» — у выхода по таймеру его нет. */
  selfEnd: PlayerSelfEndSessionResponse | null;
}

/**
 * Сессия, за которой сидел вошедший, закрылась — по таймеру, у стойки или им самим. Итог нужен в
 * любом случае: «сколько сыграл и сколько потратил» не должно прятаться в истории кошелька.
 * Вышел из аккаунта — итог уже не его.
 */
export function endedSessionId(
  previous: { screen: ShellScreen; sessionId: string | null },
  current: ShellScreen,
  signedIn: boolean
): string | null {
  if (!signedIn || !previous.sessionId) return null;
  return isSessionScreen(previous.screen) && current === 'chooseTime' ? previous.sessionId : null;
}

/** Сыграно по чеку, в минутах; меньше минуты — всё равно минута. */
export function playedMinutes(receipt: Pick<PlayerVisitReceiptDto, 'startedAtUtc' | 'endedAtUtc'>): number | null {
  if (!receipt.endedAtUtc) return null;
  const minutes = Math.floor((Date.parse(receipt.endedAtUtc) - Date.parse(receipt.startedAtUtc)) / 60_000);
  return Number.isFinite(minutes) ? Math.max(1, minutes) : null;
}
