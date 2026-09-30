import type { PlayerSelfEndSessionResponse, PlayerVisitReceiptDto } from '@afk4/contracts';
import { isSessionScreen, type ShellScreen } from './screen';

/** Визит, который только что кончился: по нему итог, чек и оценка. */
export interface EndedVisit {
  sessionId: string;
  /** Ответ на «Встать раньше» — у выхода по таймеру его нет. */
  selfEnd: PlayerSelfEndSessionResponse | null;
  /** Когда сессия кончилась, по часам ПК: от него живёт окно входа после сессии. */
  endedAtMs: number;
  /** Когда началась — из состояния ПК; по ней итог говорит, сколько сыграно, когда чека нет. */
  startedAtUtc: string | null;
}

/**
 * Сколько после конца сессии живёт вход игрока: сервер гасит токены через 30 с
 * (`DeviceBoundPlayerTokens.SummaryWindow`). Итог уходит чуть раньше — иначе оценка и чаевые после
 * этого срока отвечали бы «не получилось», а «Играть ещё» вела бы на экран, где всё отказывает.
 */
export const SUMMARY_TOKEN_WINDOW_MS = 27_000;

/**
 * Сессия вошедшего закрылась — по таймеру, у стойки или им самим. Итог нужен в любом случае:
 * «сколько сыграл и сколько потратил» не должно прятаться в истории кошелька. Вышел из аккаунта —
 * итог уже не его. Сессия была чужая (стойка посадила сюда другого игрока или гостя) — тоже: чек
 * чужого визита вошедшему не покажут, а «Сыграно …» было бы не о нём.
 */
export function endedSessionId(
  previous: { screen: ShellScreen; sessionId: string | null; ownerPlayerAccountId: string | null },
  current: ShellScreen,
  signedInPlayerAccountId: string | null
): string | null {
  if (!signedInPlayerAccountId || !previous.sessionId || previous.ownerPlayerAccountId !== signedInPlayerAccountId) return null;
  return isSessionScreen(previous.screen) && current === 'chooseTime' ? previous.sessionId : null;
}

/**
 * Сколько сыграно, в минутах: по чеку, а без чека — по началу сессии и её концу на ПК. Меньше
 * минуты — всё равно минута. Оплаченные минуты сюда не подставляются: при минимуме тарифа в час
 * они сказали бы «Сыграно 1 ч» тому, кто посидел двадцать минут.
 */
export function playedMinutes(
  receipt: Pick<PlayerVisitReceiptDto, 'startedAtUtc' | 'endedAtUtc'> | null,
  visit: Pick<EndedVisit, 'startedAtUtc' | 'endedAtMs'>
): number | null {
  const started = receipt ? receipt.startedAtUtc : visit.startedAtUtc;
  const endedAtUtc = receipt ? receipt.endedAtUtc : null;
  if (!started || (receipt && !endedAtUtc)) return null;
  const ended = endedAtUtc ? Date.parse(endedAtUtc) : visit.endedAtMs;
  const minutes = Math.floor((ended - Date.parse(started)) / 60_000);
  return Number.isFinite(minutes) ? Math.max(1, minutes) : null;
}
