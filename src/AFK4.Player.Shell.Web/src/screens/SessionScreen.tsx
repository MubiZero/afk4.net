import { useState } from 'react';
import {
  ShellBridgeRequestTypeNames,
  type LauncherAppDto,
  type PlayerSelfEndSessionResponse,
  type PlayerShellStateDto,
  type ShellAuthStateDto,
  type ShellSystemStateDto
} from '@afk4/contracts';
import { useI18n } from '@afk4/i18n';
import { AlertTriangle, WifiOff } from 'lucide-react';
import { apiBaseUrl } from '../api/playerApi';
import { requestHost } from '../host/shellHost';
import { clubTime } from '../model/offers';
import { sessionRole } from '../model/session';
import { SeatBadge } from '../ui/SeatBadge';
import { SystemControls } from '../ui/SystemControls';
import { EndEarlySheet } from './session/EndEarlySheet';
import { ExtendSheet } from './session/ExtendSheet';
import { TimeMoneyColumn } from './session/TimeMoneyColumn';

interface SessionScreenProps {
  state: PlayerShellStateDto;
  receivedAtMs: number | null;
  variant: 'session' | 'ending' | 'grace';
  /** Звук, микрофон, раскладка: системной строки в сессии нет, кнопки живут в шапке. */
  system?: ShellSystemStateDto | null;
  auth?: ShellAuthStateDto;
  /** Владелец сессии не вошёл на ПК — открыть окно входа поверх сессии. */
  onSignIn?: () => void;
  /** Встал раньше — итог показывает следующий экран. */
  onEnded?: (result: PlayerSelfEndSessionResponse) => void;
}

const signedOut: ShellAuthStateDto = { signedIn: false, displayName: null, playerAccountId: null };

/**
 * Идёт оплаченная сессия (кадр 03): игры клуба и колонка «время и деньги» — продлить, встать
 * раньше. Вкладки бара и пополнения — срез P4c-3.
 */
export function SessionScreen({
  state,
  receivedAtMs,
  variant,
  system = null,
  auth = signedOut,
  onSignIn = () => {},
  onEnded = () => {}
}: SessionScreenProps) {
  const { t, locale } = useI18n();
  const [sheet, setSheet] = useState<'extend' | 'end' | null>(null);
  const [extendedUntil, setExtendedUntil] = useState<string | null>(null);
  const baseUrl = apiBaseUrl(state);
  const role = sessionRole(state, auth);
  const offline = variant === 'grace' || !state.isOnline;

  return (
    <main className="session-screen">
      <header className="session-screen__top">
        <SeatBadge seatLabel={state.seatLabel} zoneName={state.zoneName} />
        <SystemControls system={system} />
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

      {extendedUntil ? (
        <p className="banner banner--success" role="status">
          {t('playerShell.extend.done', { time: clubTime(extendedUntil, undefined, locale) })}
        </p>
      ) : null}

      <div className="session-screen__body">
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

      <TimeMoneyColumn
        state={state}
        receivedAtMs={receivedAtMs}
        role={role}
        offline={offline || !baseUrl}
        onExtend={() => setSheet('extend')}
        onEndEarly={() => setSheet('end')}
        onSignIn={onSignIn}
        onSignOut={() => void requestHost(ShellBridgeRequestTypeNames.AuthSignOut).catch(() => {})}
      />
      </div>

      {sheet === 'extend' && baseUrl && state.sessionId ? (
        <ExtendSheet
          baseUrl={baseUrl}
          sessionId={state.sessionId}
          onClose={() => setSheet(null)}
          onExtended={(endsAtUtc) => {
            setSheet(null);
            setExtendedUntil(endsAtUtc);
          }}
        />
      ) : null}
      {sheet === 'end' && baseUrl && state.sessionId ? (
        <EndEarlySheet
          baseUrl={baseUrl}
          sessionId={state.sessionId}
          onClose={() => setSheet(null)}
          onEnded={(result) => {
            setSheet(null);
            onEnded(result);
          }}
        />
      ) : null}
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
