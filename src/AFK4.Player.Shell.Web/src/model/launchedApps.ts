import { ShellBridgeErrorCodeNames, ShellPipeErrorCodeNames } from '@afk4/contracts';
import { HostBridgeRequestError } from '@afk4/host-bridge';
import type { MessageKey } from '@afk4/i18n';

/**
 * Отказ «Вернуться» или «Закрыть» — словами по коду хоста и агента. Окно игры могло ещё не
 * открыться (Steam поднимается десятки секунд), а само приложение — уже закрыться: это два разных
 * ответа, и «не получилось» не объясняет ни один.
 */
export function appActionErrorKey(reason: unknown): MessageKey {
  const code = reason instanceof HostBridgeRequestError ? reason.code : null;
  switch (code) {
    case ShellBridgeErrorCodeNames.AppWindowNotFound:
      return 'playerShell.apps.error.window';
    case ShellPipeErrorCodeNames.AppNotRunning:
      return 'playerShell.apps.error.gone';
    default:
      return 'playerShell.session.error.generic';
  }
}
