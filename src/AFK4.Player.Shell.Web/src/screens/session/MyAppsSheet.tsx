import { useState } from 'react';
import {
  ShellBridgeRequestTypeNames,
  type LauncherAppDto,
  type LaunchedAppDto
} from '@afk4/contracts';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { requestHost } from '../../host/shellHost';
import { appActionErrorKey } from '../../model/launchedApps';
import { Sheet } from '../../ui/Sheet';

interface MyAppsSheetProps {
  /** Запущенное игроком из библиотеки в этой сессии — от агента, без системных процессов. */
  apps: readonly LaunchedAppDto[];
  /** Библиотека: обложка запущенной игры берётся из плитки. */
  library: readonly LauncherAppDto[];
  onClose: () => void;
}

/**
 * «Мои приложения» — свой диспетчер задач игрока. Диспетчер Windows в сессии выключен: из него
 * можно закрыть оболочку или чужой процесс. Здесь только то, что игрок запустил сам, и два
 * действия на каждую строку: вернуться в окно игры и закрыть её вместе с дочерними процессами.
 */
export function MyAppsSheet({ apps, library, onClose }: MyAppsSheetProps) {
  const { t } = useI18n();
  const [closing, setClosing] = useState<ReadonlySet<string>>(new Set());
  const [error, setError] = useState<MessageKey | null>(null);

  const comeBack = async (app: LaunchedAppDto) => {
    setError(null);
    try {
      await requestHost(ShellBridgeRequestTypeNames.AppFocus, { launchId: app.launchId });
      // Игра вышла вперёд, оболочка отступила: лист за ней остался бы открытым на следующий раз.
      onClose();
    } catch (reason) {
      setError(appActionErrorKey(reason));
    }
  };

  const close = async (app: LaunchedAppDto) => {
    setError(null);
    setClosing((current) => new Set(current).add(app.launchId));
    try {
      await requestHost(ShellBridgeRequestTypeNames.AppClose, { launchId: app.launchId });
      // Строка уйдёт сама, когда агент пришлёт состояние без этой игры.
    } catch (reason) {
      setError(appActionErrorKey(reason));
      setClosing((current) => {
        const next = new Set(current);
        next.delete(app.launchId);
        return next;
      });
    }
  };

  return (
    <Sheet title={t('playerShell.apps.title')} onClose={onClose}>
      {apps.length === 0 ? (
        <p className="library__empty">{t('playerShell.apps.empty')}</p>
      ) : (
        <ul className="my-apps">
          {apps.map((app) => {
            const cover = library.find((tile) => tile.appId === app.appId)?.iconUri;
            const isClosing = closing.has(app.launchId);
            return (
              <li key={app.launchId} className="my-apps__row">
                {cover
                  ? <img className="my-apps__cover" src={cover} alt="" />
                  : <span className="my-apps__cover my-apps__cover--name" aria-hidden="true">{app.displayName.slice(0, 1)}</span>}
                <span className="my-apps__name">{app.displayName}</span>
                <button
                  type="button"
                  className="btn btn--primary"
                  disabled={isClosing}
                  aria-label={`${t('playerShell.apps.return')}: ${app.displayName}`}
                  onClick={() => void comeBack(app)}
                >
                  {t('playerShell.apps.return')}
                </button>
                <button
                  type="button"
                  className="btn btn--ghost"
                  disabled={isClosing}
                  aria-label={`${t('playerShell.apps.close')}: ${app.displayName}`}
                  onClick={() => void close(app)}
                >
                  {isClosing ? t('playerShell.apps.closing') : t('playerShell.apps.close')}
                </button>
              </li>
            );
          })}
        </ul>
      )}

      {error ? <p className="banner banner--danger" role="alert">{t(error)}</p> : null}
      <p className="my-apps__hint">{t('playerShell.apps.hint')}</p>
      <p className="my-apps__hint">{t('playerShell.apps.hotkey')}</p>
    </Sheet>
  );
}
