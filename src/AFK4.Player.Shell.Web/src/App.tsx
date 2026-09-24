import { useEffect, useRef, useState, type CSSProperties } from 'react';
import { useI18n, isLocale } from '@afk4/i18n';
import { AlertOctagon, Loader2, WifiOff, Wrench } from 'lucide-react';
import { useShellHost } from './host/shellHost';
import { clubAccent } from './model/branding';
import { selectScreen } from './model/screen';
import { ChooseTimeScreen } from './screens/ChooseTimeScreen';
import { IdleScreen } from './screens/IdleScreen';
import { SessionScreen } from './screens/SessionScreen';
import { SignInPanel } from './screens/SignInPanel';
import { StatusScreen } from './screens/StatusScreen';
import { AssistButton } from './ui/AssistButton';
import { SeatBadge } from './ui/SeatBadge';
import { SystemBar } from './ui/SystemBar';

export function App() {
  const { t, setLocale } = useI18n();
  const host = useShellHost();
  const { state } = host;
  const [approached, setApproached] = useState(false);

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

  const screen = selectScreen({ state, signedIn: host.auth.signedIn, approached });
  const online = state?.isOnline ?? false;

  return (
    <div className="shell" data-screen={screen} style={shellStyle}>
      {renderScreen()}
      {screen === 'session' || screen === 'ending' || screen === 'grace'
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
        return (
          <StatusScreen
            top={seat}
            icon={<Wrench />}
            title={t('playerShell.maintenance.title')}
            body={t('playerShell.maintenance.body')}
          />
        );
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
        return <SessionScreen state={state!} receivedAtMs={host.stateReceivedAtMs} variant={screen} system={host.system} />;
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
