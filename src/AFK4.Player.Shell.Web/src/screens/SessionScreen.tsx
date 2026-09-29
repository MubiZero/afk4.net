import { useEffect, useId, useRef, useState } from 'react';
import {
  PlatformFeatureNames,
  ShellBridgeRequestTypeNames,
  type LauncherAppDto,
  type PlayerSelfEndSessionResponse,
  type PlayerShellStateDto,
  type ShellAuthStateDto,
  type ShellSystemStateDto
} from '@afk4/contracts';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { AlertTriangle, CheckCircle2, Play, WifiOff } from 'lucide-react';
import { apiBaseUrl } from '../api/playerApi';
import { requestHost } from '../host/shellHost';
import { isOrderActive } from '../model/bar';
import { clubTime } from '../model/offers';
import { sessionRole } from '../model/session';
import { SeatBadge } from '../ui/SeatBadge';
import { SystemControls } from '../ui/SystemControls';
import { BarTab } from './session/BarTab';
import { EndEarlySheet } from './session/EndEarlySheet';
import { ExtendSheet } from './session/ExtendSheet';
import { TimeMoneyColumn } from './session/TimeMoneyColumn';
import { TopUpPanel } from './session/TopUpPanel';
import { useBarOrders } from './session/useBarOrders';

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
/** Ключи — зеркало `PlayerShellWarningKinds` (C#): кодоген переносит только классы `*Names`. */
const WARNING_KEY: Partial<Record<string, MessageKey>> = {
  low_balance: 'playerShell.warning.lowBalance',
  credit_limit: 'playerShell.warning.creditLimit'
};

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
  const warningKey = state.warningKind ? WARNING_KEY[state.warningKind] : undefined;
  const bar = useBarOrders(baseUrl, barAvailable);
  const activeOrder = barAvailable ? bar.orders.find(isOrderActive) ?? null : null;

  // «Продлено до …» — подтверждение, а не вывеска: через полминуты уходит, остаток и так в колонке.
  useEffect(() => {
    if (!extendedUntil) return undefined;
    const timer = window.setTimeout(() => setExtendedUntil(null), 30_000);
    return () => window.clearTimeout(timer);
  }, [extendedUntil]);

  return (
    <main className="session-screen">
      {/* Полосы — в шапке, поверх пустого места между номером ПК и звуком: раньше каждая
          вставала строкой над телом и сдвигала таймер и кнопки вниз, пока не уйдёт. */}
      <header className="session-screen__top">
        <SeatBadge seatLabel={state.seatLabel} zoneName={state.zoneName} />
        <div className="session-screen__notices">
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
              <CheckCircle2 aria-hidden="true" />
              {t('playerShell.extend.done', { time: clubTime(extendedUntil, undefined, locale) })}
            </p>
          ) : null}
          {/* Предупреждения агента: деньги кончаются, упёрлись в лимит долга. Связь и «мало
              времени» уже сказаны полосами выше — второй раз не повторяем. */}
          {warningKey ? (
            <p className="banner banner--warning" role="status">
              <AlertTriangle aria-hidden="true" />
              {t(warningKey)}
            </p>
          ) : null}
        </div>
        <SystemControls system={system} />
      </header>

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
            <BarTab baseUrl={baseUrl} orders={bar.orders} onOrderChanged={bar.apply} reloadOrders={bar.reload} />
          </section>
        ) : topUpAvailable && tab === 'topUp' && baseUrl ? (
          <section className="session-panel" role="tabpanel" id={`${tabsId}-panel`} aria-labelledby={`${tabsId}-topUp`}>
            <TopUpPanel baseUrl={baseUrl} onDone={() => setTab('games')} />
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
        signedIn={auth.signedIn}
        activeOrder={tab === 'bar' ? null : activeOrder}
        onOpenBar={() => setTab('bar')}
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

/**
 * Сколько плитка держит «Запускается…» после ответа хоста. Хост отвечает, как только процесс
 * создан, а окно игры появляется через секунды: вернись кнопка сразу — человек нажал бы второй раз
 * и получил две копии игры.
 */
const LAUNCH_SETTLE_MS = 8000;

function LibraryTile({ app }: { app: LauncherAppDto }) {
  const { t } = useI18n();
  const [launch, setLaunch] = useState<LaunchState>('idle');
  const settle = useRef<number | null>(null);
  useEffect(() => () => {
    if (settle.current !== null) window.clearTimeout(settle.current);
  }, []);

  const start = async () => {
    if (launch === 'launching') return;
    setLaunch('launching');
    try {
      await requestHost(ShellBridgeRequestTypeNames.AppLaunch, { appId: app.appId });
      settle.current = window.setTimeout(() => setLaunch('idle'), LAUNCH_SETTLE_MS);
    } catch {
      setLaunch('failed');
    }
  };

  const cover = app.iconUri
    ? <img className="library-tile__cover" src={app.iconUri} alt="" loading="lazy" decoding="async" />
    : <span className="library-tile__cover library-tile__cover--name" aria-hidden="true">{app.displayName.slice(0, 1)}</span>;
  const title = (
    <span className="library-tile__name">
      {app.displayName}
      {app.minAge ? <span className="library-tile__age">{app.minAge}+</span> : null}
    </span>
  );

  // Недоступную игру не нажать: плитка — не кнопка, и причина написана на ней.
  if (app.ageLocked || !app.isAvailable) {
    return (
      <div className={app.ageLocked ? 'library-tile library-tile--locked' : 'library-tile library-tile--unavailable'}>
        {cover}
        {title}
        <span className="library-tile__category">{app.category}</span>
        <span className="library-tile__missing">
          {app.ageLocked
            // Возраст из дня рождения в профиле: агент такую игру и не запустит.
            ? t('playerShell.session.ageLocked', { age: app.minAge ?? 0 })
            : t('playerShell.session.unavailable')}
        </span>
      </div>
    );
  }

  // Нажимается вся плитка, как в любом лаунчере: раньше попасть надо было в кнопку «Играть»
  // внизу. Сама кнопка видна на наведении и фокусе — остальное время плитку читают по обложке.
  const launchLabel = launch === 'launching' ? t('playerShell.session.launching') : t('playerShell.session.launch');
  return (
    <>
      <button
        type="button"
        className="library-tile library-tile--playable"
        data-launching={launch === 'launching' || undefined}
        aria-label={`${launchLabel}: ${app.displayName}`}
        onClick={start}
        disabled={launch === 'launching'}
      >
        <span className="library-tile__art">
          {cover}
          <span className="library-tile__play btn btn--primary" aria-hidden="true">
            <Play aria-hidden="true" />
            {launchLabel}
          </span>
        </span>
        {title}
        <span className="library-tile__category">{app.category}</span>
      </button>
      {launch === 'failed' ? <p className="banner banner--danger library-tile__error" role="alert">{t('playerShell.session.launchFailed')}</p> : null}
    </>
  );
}
