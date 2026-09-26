import { PlayerShellStateNames, type PlayerShellStateDto } from '@afk4/contracts';

/**
 * Экраны оболочки (спека, §3). Адресов страниц нет: экран — это функция от состояния агента,
 * входа игрока и локального режима. Так экран не может «застрять» на странице, которой уже не
 * соответствует ни одно состояние ПК.
 */
export type ShellScreen =
  | 'connecting'
  | 'idle'
  | 'approach'
  | 'chooseTime'
  | 'summary'
  | 'session'
  | 'ending'
  | 'grace'
  | 'offline'
  | 'maintenance'
  | 'error';

export interface ScreenInput {
  /** Состояние от агента; null — агент ещё ничего не прислал. */
  state: PlayerShellStateDto | null;
  /** Вошёл ли игрок на этом ПК. */
  signedIn: boolean;
  /** Мышь или клавиатура тронуты на свободном ПК: витрина уступает место окну входа. */
  approached: boolean;
  /** Игрок только что встал раньше: запертый ПК сначала показывает ему итог. */
  ended?: boolean;
}

export function selectScreen({ state, signedIn, approached, ended = false }: ScreenInput): ShellScreen {
  if (state === null) return 'connecting';

  switch (state.state) {
    case PlayerShellStateNames.Maintenance:
      return 'maintenance';
    case PlayerShellStateNames.Error:
      return 'error';
    case PlayerShellStateNames.Offline:
      return 'offline';
    case PlayerShellStateNames.Grace:
      return 'grace';
    case PlayerShellStateNames.Ending:
      return 'ending';
    case PlayerShellStateNames.Active:
      return 'session';
    default:
      // Заперт: вставший раньше видит итог, вошедший выбирает время, подошедший — входит,
      // остальным крутится витрина.
      if (signedIn && ended) return 'summary';
      if (signedIn) return 'chooseTime';
      return approached ? 'approach' : 'idle';
  }
}

/** Экраны, на которых идёт оплаченная сессия: отсчёт, игры, продление. */
export function isSessionScreen(screen: ShellScreen): boolean {
  return screen === 'session' || screen === 'ending' || screen === 'grace';
}
