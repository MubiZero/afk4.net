import { useCallback, useEffect, useRef, useState, type CSSProperties } from 'react';
import { ShellBridgeRequestTypeNames } from '@afk4/contracts';
import { apiBaseUrl } from './api/playerApi';
import { useI18n, isLocale } from '@afk4/i18n';
import { AlertOctagon, Loader2, WifiOff } from 'lucide-react';
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
  const lastActivity = useRef(host.activity);
  useEffect(() => {
    if (host.activity !== lastActivity.current) {
      lastActivity.current = host.activity;
      setApproached(true);
    }
  }, [host.activity]);
  useEffect(() => {
    if (host.idle > 0) setApproached(false);
  }, [host.idle]);

  // Язык филиала — пока человек не выбрал свой. Флаг выбора — ref, а не состояние: эффект от
  // пришедшего состояния может выполниться уже после клика «Тоҷ» (React откладывает эффекты), и
  // со значением из замыкания он вернул бы язык филиала поверх выбора человека.
  const localeChosen = useRef(false);
  const branchLocale = state?.locale;
  useEffect(() => {
    if (!localeChosen.current && branchLocale && isLocale(branchLocale)) setLocale(branchLocale);
  }, [branchLocale, setLocale]);

  // Цвет клуба — поверх палитры, если его можно читать; иначе остаётся фирменный зелёный.
  const accent = clubAccent(state?.branding?.accentColor);
  const shellStyle = accent ? ({ '--club-accent': accent } as CSSProperties) : undefined;

  const screen = selectScreen({ state, signedIn: host.auth.signedIn, approached, ended: ended !== null });

  // Сессия вошедшего закрылась сама — по таймеру или у стойки: итог нужен и тогда. Смотрим на экран
  // без учёта итога, иначе он сам себя и перекрывал бы.
  const baseScreen = selectScreen({ state, signedIn: host.auth.signedIn, approached });
  const previous = useRef<{ screen: ShellScreen; sessionId: string | null }>({ screen: baseScreen, sessionId: null });
  useEffect(() => {
    const sessionId = endedSessionId(previous.current, baseScreen, host.auth.signedIn);
    if (sessionId) setEnded((current) => current ?? { sessionId, selfEnd: null });
    previous.current = { screen: baseScreen, sessionId: state?.sessionId ?? previous.current.sessionId };
  }, [baseScreen, host.auth.signedIn, state?.sessionId]);
  const online = state?.isOnline ?? false;

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
        return (
          <StatusScreen icon={<Loader2 className="spin" />} title={t('playerShell.connecting.title')} />
        );
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
              onEnded={(selfEnd) => state?.sessionId && setEnded({ sessionId: state.sessionId, selfEnd })}
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
      case 'approach':
        return (
          <>
            <IdleScreen state={state!} dimmed />
            <SignInPanel state={state!} onClose={() => setApproached(false)} />
          </>
        );
      default:
        return <IdleScreen state={state!} />;
    }
  }
}
