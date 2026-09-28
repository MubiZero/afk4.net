import {
  DeviceSessionOwnerKindNames,
  PlayerOfferUnavailableReasonNames,
  type PlayerShellStateDto,
  type ShellAuthStateDto
} from '@afk4/contracts';
import type { MessageKey } from '@afk4/i18n';
import { PlayerApiError } from '../api/playerApi';

/**
 * Кто перед экраном идущей сессии — и что ему можно делать с деньгами.
 *
 * - `owner` — вошёл тот, на чьём счёте сессия: продлить и встать раньше можно здесь.
 * - `signInToManage` — сессия на счёте игрока, но на ПК никто не вошёл (например, сел с телефона):
 *   продлить можно, войдя.
 * - `otherPlayer` — сессия на счёте игрока, а вошёл другой: чужими деньгами он не распоряжается,
 *   и ему нужен выход, чтобы владелец вошёл сам.
 * - `counter` — сессию открыла стойка без счёта игрока: продлевает администратор.
 * - `pending` — сессия только началась, и чья она, агент узнает со следующим сердцебиением:
 *   секунды без денежных кнопок лучше подсказки, которая к человеку не относится.
 */
export type SessionRole = 'owner' | 'signInToManage' | 'otherPlayer' | 'counter' | 'pending';

export function sessionRole(state: PlayerShellStateDto, auth: ShellAuthStateDto): SessionRole {
  if (state.sessionOwnerKind == null) return 'pending';
  if (state.sessionOwnerKind !== DeviceSessionOwnerKindNames.Player) return 'counter';
  if (!auth.signedIn) return 'signInToManage';
  return auth.playerAccountId != null && auth.playerAccountId === state.sessionOwnerPlayerAccountId ? 'owner' : 'otherPlayer';
}

/** Почему продлить отсюда нельзя — словами, а не пустым листом. */
export function extendUnavailableKey(reason: string | null | undefined): MessageKey | null {
  switch (reason) {
    case PlayerOfferUnavailableReasonNames.PackageSession:
      return 'playerShell.extend.unavailable.package';
    case PlayerOfferUnavailableReasonNames.NotPrepaid:
      return 'playerShell.extend.unavailable.counter';
    default:
      return null;
  }
}

/** Отказ продления или раннего выхода — по коду сервера. */
export function sessionActionErrorKey(reason: unknown): MessageKey {
  if (!(reason instanceof PlayerApiError)) return 'playerShell.chooseTime.error.offline';
  switch (reason.code) {
    case 'insufficient_balance':
      return 'playerShell.chooseTime.error.insufficient';
    case 'stale_version':
      return 'playerShell.session.error.changed';
    default:
      return reason.status === 404 ? 'playerShell.session.error.gone' : 'playerShell.session.error.generic';
  }
}
