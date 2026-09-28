import type { PlayerSelfEndSessionResponse, PlayerVisitReceiptDto } from '@afk4/contracts';
import { isSessionScreen, type ShellScreen } from './screen';

/** Визит, который только что кончился: по нему итог, чек и оценка. */
export interface EndedVisit {
  sessionId: string;
  /** Ответ на «Встать раньше» — у выхода по таймеру его нет. */
  selfEnd: PlayerSelfEndSessionResponse | null;
  /** Когда сессия кончилась, по часам ПК: от него живёт окно входа после сессии. */
  endedAtMs: number;
}

/**
 * Сколько после конца сессии живёт вход игрока: сервер гасит токены через 30 с
 * (`DeviceBoundPlayerTokens.SummaryWindow`). Итог уходит чуть раньше — иначе оценка и чаевые после
 * этого срока отвечали бы «не получилось», а «Играть ещё» вела бы на экран, где всё отказывает.
 */
export const SUMMARY_TOKEN_WINDOW_MS = 27_000;

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
