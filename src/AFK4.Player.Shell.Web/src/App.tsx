import { useCallback, useEffect, useRef, useState, type CSSProperties } from 'react';
import { ShellBridgeRequestTypeNames } from '@afk4/contracts';
import { PLAYER_UNAUTHORIZED_EVENT, apiBaseUrl } from './api/playerApi';
import { useI18n, isLocale } from '@afk4/i18n';
import { AlertOctagon, Loader2, Lock, Pause, WifiOff } from 'lucide-react';
import { requestHost, useShellHost } from './host/shellHost';
import { clubAccent } from './model/branding';
import { selectScreen, type ShellScreen } from './model/screen';
import { endedSessionId, type EndedVisit } from './model/visit';
import { ChooseTimeScreen } from './screens/ChooseTimeScreen';
import { IdleScreen } from './screens/IdleScreen';
import { MaintenanceBand } from './screens/MaintenanceBand';
import { SessionScreen } from './screens/SessionScreen';
import { SignInPanel } from './screens/SignInPanel';
import { StatusScreen } from './screens/StatusScreen';
import { SummaryScreen } from './screens/SummaryScreen';
import { AssistButton } from './ui/AssistButton';
import { SeatBadge } from './ui/SeatBadge';
import { SystemBar } from './ui/SystemBar';

/**
 * Сколько ждать службу ПК, прежде чем сказать, что она не отвечает. Обычно состояние приходит за
 * секунду после запуска; двадцать — уже не «подключаемся», а сбой, и крутить колесо дальше — молчать.
 */
export const CONNECTING_STUCK_MS = 20_000;

function ConnectingScreen() {
  const { t } = useI18n();
  const [stuck, setStuck] = useState(false);
  useEffect(() => {
    const timer = window.setTimeout(() => setStuck(true), CONNECTING_STUCK_MS);
    return () => window.clearTimeout(timer);
  }, []);
  // Позвать администратора кнопкой нельзя: вызов идёт через ту самую службу, что молчит.
  return (
    <StatusScreen
      icon={<Loader2 className="spin" />}
      title={t('playerShell.connecting.title')}
      body={stuck ? t('playerShell.connecting.stuck') : undefined}
    />
  );
}

/** Сколько после закрытия окна входа ввод не считается подходом: дольше шага сигналов хоста. */
const DISMISS_GRACE_MS = 2000;

export function App() {
  const { t, setLocale } = useI18n();
  const host = useShellHost();
  const { state } = host;
  const [approached, setApproached] = useState(false);
  // Визит, который только что кончился: по нему итог, чек и оценка — после любого конца сессии.
  const [ended, setEnded] = useState<EndedVisit | null>(null);
  // Владелец сессии, севший с телефона, входит поверх экрана сессии, чтобы продлить.
  const [signingIn, setSigningIn] = useState(false);

  useEffect(() => {
    if (host.auth.signedIn) setSigningIn(false);
  }, [host.auth.signedIn]);

  // Вышел — итог больше не его: следующий за этим ПК его не увидит.
  useEffect(() => {
    if (!host.auth.signedIn) setEnded(null);
  }, [host.auth.signedIn]);

  const leaveAfterSummary = useCallback(() => {
    setEnded(null);
    void requestHost(ShellBridgeRequestTypeNames.AuthSignOut).catch(() => {});
  }, []);

  // Подошли к свободному ПК — витрина уступает место окну входа; отошли — возвращается.
  // Закрыли окно сами (Esc, «Назад») — эта клавиша тоже ввод, и хост сообщит о нём с опозданием до
  // секунды (`InputActivityTracker.DefaultActivityEvery`). Такой сигнал — не «подошли снова».
  const lastActivity = useRef(host.activity);
  const dismissedAtMs = useRef(0);
  useEffect(() => {
    if (host.activity !== lastActivity.current) {
      lastActivity.current = host.activity;
      if (Date.now() - dismissedAtMs.current >= DISMISS_GRACE_MS) setApproached(true);
    }
  }, [host.activity]);
  const dismissSignIn = useCallback(() => {
    dismissedAtMs.current = Date.now();
    setApproached(false);
  }, []);
  // Минута тишины: витрина возвращается, а вошедший, но так и не начавший сессию, выходит. Сервер
  // гасит такой вход только через 5 минут — и всё это время подошедший следом начал бы сессию на
  // чужие деньги (`DeviceBoundPlayerTokens.PreSessionWindow`).
  const screenNow = useRef<ShellScreen>('connecting');
  useEffect(() => {
    if (host.idle === 0) return;
    setApproached(false);
    if (screenNow.current === 'chooseTime') void requestHost(ShellBridgeRequestTypeNames.AuthSignOut).catch(() => {});
  }, [host.idle]);

  // Сервер отказал входу (401). Это не обязательно конец: доступ живёт 15 минут и без связи успевает
  // истечь, а первый же запрос после её возврата приходит раньше круга обновления на хосте. Выходить
  // сразу — выкинуть игрока из идущей сессии. Решает хост: идёт за новым доступом, и только если
  // сервер отказал и обновлению (токены погашены сессией или новым входом), забывает вход и
  // присылает auth.changed — экран с чужим именем и балансом тогда уходит сам.
  const refreshingAuth = useRef(false);
  useEffect(() => {
    const onUnauthorized = () => {
      // Пачка запросов с одним протухшим токеном даёт пачку 401 — обновлять достаточно один раз.
      if (refreshingAuth.current) return;
      refreshingAuth.current = true;
      void requestHost(ShellBridgeRequestTypeNames.AuthRefresh)
        .catch(() => {})
        .finally(() => { refreshingAuth.current = false; });
    };
    window.addEventListener(PLAYER_UNAUTHORIZED_EVENT, onUnauthorized);
    return () => window.removeEventListener(PLAYER_UNAUTHORIZED_EVENT, onUnauthorized);
  }, []);

  // Язык филиала — пока человек не выбрал свой. Флаг выбора — ref, а не состояние: эффект от
  // пришедшего состояния может выполниться уже после клика «Тоҷ» (React откладывает эффекты), и
  // со значением из замыкания он вернул бы язык филиала поверх выбора человека.
  const localeChosen = useRef(false);
  const branchLocale = state?.locale;
  useEffect(() => {
    if (!localeChosen.current && branchLocale && isLocale(branchLocale)) setLocale(branchLocale);
  }, [branchLocale, setLocale]);

  // Выбор языка — этого игрока: вышел — следующий видит язык клуба, а не чужой выбор.
  const signedIn = host.auth.signedIn;
  const wasSignedIn = useRef(signedIn);
  useEffect(() => {
    if (wasSignedIn.current && !signedIn) {
      localeChosen.current = false;
      if (branchLocale && isLocale(branchLocale)) setLocale(branchLocale);
    }
    wasSignedIn.current = signedIn;
  }, [signedIn, branchLocale, setLocale]);

  // Цвет клуба — поверх палитры, если его можно читать; иначе остаётся фирменный зелёный.
  const accent = clubAccent(state?.branding?.accentColor);
  const shellStyle = accent ? ({ '--club-accent': accent } as CSSProperties) : undefined;

  const screen = selectScreen({ state, signedIn: host.auth.signedIn, approached, ended: ended !== null });
  screenNow.current = screen;

  // Сессия вошедшего закрылась сама — по таймеру или у стойки: итог нужен и тогда. Смотрим на экран
  // без учёта итога, иначе он сам себя и перекрывал бы.
  const baseScreen = selectScreen({ state, signedIn: host.auth.signedIn, approached });
  const previous = useRef<{ screen: ShellScreen; sessionId: string | null; ownerPlayerAccountId: string | null; startedAtUtc: string | null }>({
    screen: baseScreen, sessionId: null, ownerPlayerAccountId: null, startedAtUtc: null
  });
  const signedInAccountId = host.auth.signedIn ? host.auth.playerAccountId ?? null : null;
  const sessionOwnerAccountId = state?.sessionOwnerPlayerAccountId ?? null;
  useEffect(() => {
    const sessionId = endedSessionId(previous.current, baseScreen, signedInAccountId);
    if (sessionId) setEnded((current) => current ?? { sessionId, selfEnd: null, endedAtMs: Date.now(), startedAtUtc: previous.current.startedAtUtc });
    // Кончилась сессия — следующее состояние уже без неё: помним, чья была последняя.
    previous.current = state?.sessionId
      ? { screen: baseScreen, sessionId: state.sessionId, ownerPlayerAccountId: sessionOwnerAccountId, startedAtUtc: state.sessionStartedAtUtc ?? null }
      : { ...previous.current, screen: baseScreen };
  }, [baseScreen, signedInAccountId, state?.sessionId, sessionOwnerAccountId, state?.sessionStartedAtUtc]);
  // Состояния ещё нет — служба ПК не ответила, и связь с клубом не проверена вовсе.
  const online = state ? state.isOnline : null;

  return (
    <div className="shell" data-screen={screen} style={shellStyle}>
      {renderScreen()}
      {screen === 'session' || screen === 'ending' || screen === 'grace' || screen === 'maintenance'
        ? null
        : <SystemBar online={online} system={host.system} onLocaleChosen={() => { localeChosen.current = true; }} />}
    </div>
  );

  function renderScreen() {
    const seat = state ? <SeatBadge seatLabel={state.seatLabel} zoneName={state.zoneName} /> : null;
    switch (screen) {
      case 'connecting':
        return <ConnectingScreen />;
      case 'offline':
        return (
          <StatusScreen
            tone="warning"
            top={seat}
            icon={<WifiOff />}
            title={t('playerShell.offline.title')}
            body={t('playerShell.offline.body')}
          />
        );
      case 'paused':
        return (
          <StatusScreen
            tone="warning"
            top={seat}
            icon={<Pause />}
            title={t('playerShell.paused.title')}
            body={t('playerShell.paused.body')}
          />
        );
      case 'held':
        return (
          <StatusScreen
            tone="warning"
            top={seat}
            icon={<Lock />}
            title={t('playerShell.hold.title')}
            body={t('playerShell.hold.body')}
          />
        );
      case 'maintenance':
        return state ? <MaintenanceBand state={state} /> : null;
      case 'error':
        return (
          <StatusScreen
            tone="danger"
            top={seat}
            icon={<AlertOctagon />}
            title={t('playerShell.error.title')}
            body={t('playerShell.error.body')}
          >
            <AssistButton />
          </StatusScreen>
        );
      case 'session':
      case 'ending':
      case 'grace':
        return (
          <>
            <SessionScreen
              state={state!}
              receivedAtMs={host.stateReceivedAtMs}
              variant={screen}
              system={host.system}
              auth={host.auth}
              onSignIn={() => setSigningIn(true)}
              onEnded={(selfEnd) => state?.sessionId && setEnded({ sessionId: state.sessionId, selfEnd, endedAtMs: Date.now(), startedAtUtc: state.sessionStartedAtUtc ?? null })}
            />
            {signingIn && !host.auth.signedIn ? <SignInPanel state={state!} onClose={() => setSigningIn(false)} /> : null}
          </>
        );
      case 'summary':
        return (
          <SummaryScreen
            state={state!}
            visit={ended!}
            baseUrl={apiBaseUrl(state)}
            activity={host.activity}
            onPlayMore={() => setEnded(null)}
            onLeave={leaveAfterSummary}
          />
        );
      case 'chooseTime':
        return <ChooseTimeScreen state={state!} auth={host.auth} />;
      default:
        // Простой и «подошли» — одна витрина: окно входа ложится поверх того же экземпляра, и после
        // отхода от ПК показ продолжается с той же карточки, а не с первой.
        return (
          <>
            <IdleScreen state={state!} dimmed={screen === 'approach'} />
            {screen === 'approach' ? <SignInPanel state={state!} onClose={dismissSignIn} /> : null}
          </>
        );
    }
  }
}
