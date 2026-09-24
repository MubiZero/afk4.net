import { useState } from 'react';
import { ShellBridgeRequestTypeNames, type LauncherAppDto, type PlayerShellStateDto } from '@afk4/contracts';
import { useI18n } from '@afk4/i18n';
import { AlertTriangle, WifiOff } from 'lucide-react';
import { requestHost } from '../host/shellHost';
import { Countdown } from '../ui/Countdown';
import { SeatBadge } from '../ui/SeatBadge';

interface SessionScreenProps {
  state: PlayerShellStateDto;
  receivedAtMs: number | null;
  variant: 'session' | 'ending' | 'grace';
}

/**
 * Идёт оплаченная сессия: сколько осталось и игры клуба. Вкладки бара, пополнения, продления и
 * «Встать раньше» — срез P4c; здесь — то, без чего сессия не сессия.
 */
export function SessionScreen({ state, receivedAtMs, variant }: SessionScreenProps) {
  const { t } = useI18n();

  return (
    <main className="session-screen">
      <header className="session-screen__top">
        <SeatBadge seatLabel={state.seatLabel} zoneName={state.zoneName} />
        <div className="session-screen__time">
          <span className="session-screen__time-label">{t('playerShell.session.remaining')}</span>
          <Countdown
            className="session-screen__countdown"
            untilUtc={state.leaseExpiresAtUtc}
            observedAtUtc={state.observedAtUtc}
            receivedAtMs={receivedAtMs}
          />
        </div>
      </header>

      {variant === 'grace' ? (
        <p className="banner banner--warning" role="status">
          <WifiOff aria-hidden="true" />
          {t('playerShell.grace.banner')}
        </p>
      ) : null}
      {variant === 'ending' ? (
        <p className="banner banner--danger" role="status">
          <AlertTriangle aria-hidden="true" />
          {t('playerShell.ending.banner')}
        </p>
      ) : null}

      <section className="library" aria-labelledby="library-title">
        <h2 id="library-title" className="library__title">{t('playerShell.session.library')}</h2>
        {state.launcherApps.length === 0 ? (
          <p className="library__empty">{t('playerShell.session.libraryEmpty')}</p>
        ) : (
          <ul className="library__grid">
            {state.launcherApps.map((app) => (
              <li key={app.appId}>
                <LibraryTile app={app} />
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}

type LaunchState = 'idle' | 'launching' | 'failed';

function LibraryTile({ app }: { app: LauncherAppDto }) {
  const { t } = useI18n();
  const [launch, setLaunch] = useState<LaunchState>('idle');

  const start = async () => {
    setLaunch('launching');
    try {
      await requestHost(ShellBridgeRequestTypeNames.AppLaunch, { appId: app.appId });
      setLaunch('idle');
    } catch {
      setLaunch('failed');
    }
  };

  return (
    <div className="library-tile">
      <span className="library-tile__name">{app.displayName}</span>
      <span className="library-tile__category">{app.category}</span>
      {app.isAvailable ? (
        <button type="button" className="btn btn--primary" onClick={start} disabled={launch === 'launching'}>
          {launch === 'launching' ? t('playerShell.session.launching') : t('playerShell.session.launch')}
        </button>
      ) : (
        <span className="library-tile__missing">{t('playerShell.session.unavailable')}</span>
      )}
      {launch === 'failed' ? <p className="library-tile__error" role="alert">{t('playerShell.session.launchFailed')}</p> : null}
    </div>
  );
}
