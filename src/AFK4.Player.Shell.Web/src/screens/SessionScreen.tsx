import { useId, useState } from 'react';
import {
  PlatformFeatureNames,
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
import { BarTab } from './session/BarTab';
import { EndEarlySheet } from './session/EndEarlySheet';
import { ExtendSheet } from './session/ExtendSheet';
import { TimeMoneyColumn } from './session/TimeMoneyColumn';
import { TopUpPanel } from './session/TopUpPanel';

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
  // Бар — только владельцу, вошедшему на ПК: заказ списывается с его кошелька. И только если у
  // клуба это право по тарифу: вкладка, которая отвечает «нет доступа», хуже её отсутствия.
  const features = state.features ?? [];
  const barAvailable = role === 'owner' && Boolean(baseUrl) && features.includes(PlatformFeatureNames.PlayerShop);
  const topUpAvailable = role === 'owner' && Boolean(baseUrl) && features.includes(PlatformFeatureNames.OnlineTopUp);
  const tabsShown = barAvailable || topUpAvailable;
  const [tab, setTab] = useState<'games' | 'bar' | 'topUp'>('games');
  const tabsId = useId();

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
      <div className="session-screen__main">
        {tabsShown ? (
          <div className="tabs" role="tablist" aria-label={t('playerShell.tabs.label')}>
            <button
              type="button"
              role="tab"
              id={`${tabsId}-games`}
              aria-controls={`${tabsId}-panel`}
              aria-selected={tab === 'games'}
              className="tabs__tab"
              onClick={() => setTab('games')}
            >
              {t('playerShell.session.library')}
            </button>
            {barAvailable ? (
              <button
                type="button"
                role="tab"
                id={`${tabsId}-bar`}
                aria-controls={`${tabsId}-panel`}
                aria-selected={tab === 'bar'}
                className="tabs__tab"
                onClick={() => setTab('bar')}
              >
                {t('playerShell.tabs.bar')}
              </button>
            ) : null}
            {topUpAvailable ? (
              <button
                type="button"
                role="tab"
                id={`${tabsId}-topUp`}
                aria-controls={`${tabsId}-panel`}
                aria-selected={tab === 'topUp'}
                className="tabs__tab"
                onClick={() => setTab('topUp')}
              >
                {t('playerShell.tabs.topUp')}
              </button>
            ) : null}
          </div>
        ) : null}

        {barAvailable && tab === 'bar' && baseUrl ? (
          <section className="session-panel" role="tabpanel" id={`${tabsId}-panel`} aria-labelledby={`${tabsId}-bar`}>
            <BarTab baseUrl={baseUrl} />
          </section>
        ) : topUpAvailable && tab === 'topUp' && baseUrl ? (
          <section className="session-panel" role="tabpanel" id={`${tabsId}-panel`} aria-labelledby={`${tabsId}-topUp`}>
            <TopUpPanel baseUrl={baseUrl} />
          </section>
        ) : (
          <section
            className="library"
            {...(tabsShown
              ? { role: 'tabpanel', id: `${tabsId}-panel`, 'aria-labelledby': `${tabsId}-games` }
              : { 'aria-labelledby': 'library-title' })}
          >
            {tabsShown ? null : <h2 id="library-title" className="library__title">{t('playerShell.session.library')}</h2>}
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
        )}
      </div>

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
    <div className={app.ageLocked ? 'library-tile library-tile--locked' : 'library-tile'}>
      {/* Обложка — из кэша ПК; её нет — плитка по названию, а не пустой квадрат. */}
      {app.iconUri
        ? <img className="library-tile__cover" src={app.iconUri} alt="" loading="lazy" decoding="async" />
        : <span className="library-tile__cover library-tile__cover--name" aria-hidden="true">{app.displayName.slice(0, 1)}</span>}
      <span className="library-tile__name">
        {app.displayName}
        {app.minAge !== null && app.minAge !== undefined ? <span className="library-tile__age">{app.minAge}+</span> : null}
      </span>
      <span className="library-tile__category">{app.category}</span>
      {app.ageLocked ? (
        // Возраст из дня рождения в профиле: агент такую игру и не запустит.
        <span className="library-tile__missing">{t('playerShell.session.ageLocked', { age: app.minAge ?? 0 })}</span>
      ) : app.isAvailable ? (
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
