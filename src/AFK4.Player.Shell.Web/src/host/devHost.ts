import {
  PlayerShellStateNames,
  ShellBridgeEventTypeNames,
  ShellBridgeRequestTypeNames,
  type PlayerShellStateDto,
  type ShellAuthStateDto,
  type ShellSnapshotDto
} from '@afk4/contracts';
import type { HostBridgeMessageEvent } from '@afk4/host-bridge';

/**
 * Учебный хост: без WPF и агента экран оболочки листается в браузере сценариями —
 * `?scenario=idle|approach|session|ending|grace|offline|maintenance|error|connecting`.
 *
 * Только для dev-сборки (main.tsx подключает его под `import.meta.env.DEV`, Vite вырезает ветку
 * из боевой сборки). Образец — devHostBridge Панели.
 */
export type DevScenario =
  | 'idle'
  | 'approach'
  | 'session'
  | 'ending'
  | 'grace'
  | 'offline'
  | 'maintenance'
  | 'error'
  | 'connecting';

const SCENARIOS: readonly DevScenario[] = ['idle', 'approach', 'session', 'ending', 'grace', 'offline', 'maintenance', 'error', 'connecting'];

export function devScenarioState(scenario: DevScenario, nowMs = Date.now()): PlayerShellStateDto | null {
  if (scenario === 'connecting') return null;

  const observed = new Date(nowMs).toISOString();
  const minutes = (count: number) => new Date(nowMs + count * 60_000).toISOString();
  const base: PlayerShellStateDto = {
    organizationId: '00000000-0000-4000-8000-000000000001',
    branchId: '00000000-0000-4000-8000-000000000002',
    deviceId: '00000000-0000-4000-8000-000000000003',
    state: PlayerShellStateNames.Locked,
    sessionId: null,
    leaseExpiresAtUtc: null,
    remainingSeconds: null,
    isOnline: true,
    isGraceMode: false,
    warningThresholdSeconds: 300,
    message: '',
    launcherApps: [
      { appId: 'cs2', displayName: 'Counter-Strike 2', category: 'Шутеры', iconUri: null, isAvailable: true },
      { appId: 'dota2', displayName: 'Dota 2', category: 'MOBA', iconUri: null, isAvailable: true },
      { appId: 'valorant', displayName: 'Valorant', category: 'Шутеры', iconUri: null, isAvailable: false }
    ],
    locale: 'ru',
    warningKind: 'none',
    branding: { clubName: 'Орбита', logoUrl: null, accentColor: '#2cc592' },
    seatingCode: '418207',
    seatingCodeExpiresAtUtc: minutes(2),
    observedAtUtc: observed,
    lastContactUtc: observed,
    apiBaseUrl: 'https://api.example.test/',
    seatLabel: 'ПК 07',
    zoneName: 'Общий зал',
    sessionOwnerKind: 'none',
    sessionOwnerPlayerAccountId: null,
    features: ['player_shop', 'loyalty', 'online_topup']
  };

  const playing = (untilMinutes: number, state: string): PlayerShellStateDto => ({
    ...base,
    state,
    sessionId: '00000000-0000-4000-8000-000000000010',
    leaseExpiresAtUtc: minutes(untilMinutes),
    remainingSeconds: Math.round(untilMinutes * 60),
    seatingCode: null,
    seatingCodeExpiresAtUtc: null,
    sessionOwnerKind: 'player',
    sessionOwnerPlayerAccountId: '00000000-0000-4000-8000-000000000020'
  });

  switch (scenario) {
    case 'session':
      return playing(95, PlayerShellStateNames.Active);
    case 'ending':
      return { ...playing(0.8, PlayerShellStateNames.Ending), warningKind: 'low_time' };
    case 'grace':
      return {
        ...playing(12, PlayerShellStateNames.Grace),
        isOnline: false,
        isGraceMode: true,
        warningKind: 'connectivity',
        lastContactUtc: minutes(-3)
      };
    case 'offline':
      return { ...base, state: PlayerShellStateNames.Offline, isOnline: false, seatingCode: null, seatingCodeExpiresAtUtc: null, lastContactUtc: minutes(-4) };
    case 'maintenance':
      return { ...base, state: PlayerShellStateNames.Maintenance, seatingCode: null };
    case 'error':
      return { ...base, state: PlayerShellStateNames.Error, seatingCode: null };
    default:
      return base;
  }
}

export function installDevHost(): void {
  const params = new URLSearchParams(window.location.search);
  const requested = params.get('scenario') as DevScenario | null;
  const scenario: DevScenario = requested && SCENARIOS.includes(requested) ? requested : 'idle';
  const listeners = new Set<(event: HostBridgeMessageEvent) => void>();
  const emit = (data: unknown) => queueMicrotask(() => {
    for (const listener of listeners) listener({ data });
  });
  let auth: ShellAuthStateDto = { signedIn: false, displayName: null, playerAccountId: null };

  window.chrome = {
    webview: {
      postMessage(message: unknown) {
        const request = message as { type?: string; requestId?: string };
        if (!request?.requestId) return;
        const reply = (payload: unknown) => emit({ type: 'host:response', requestId: request.requestId, ok: true, payload });
        switch (request.type) {
          case ShellBridgeRequestTypeNames.ShellReady:
            reply({ state: devScenarioState(scenario), auth, system: { volume: 60, micMuted: false, layout: 'RU' } } satisfies ShellSnapshotDto);
            break;
          case ShellBridgeRequestTypeNames.AuthSignIn: {
            // Учебный вход: ПИН-код 123456 пускает, остальные — нет, как ответил бы сервер.
            const { pin } = (message as { payload?: { pin?: string } }).payload ?? {};
            if (pin === '123456') {
              reply({});
              auth = { signedIn: true, displayName: 'Алишер', playerAccountId: '00000000-0000-4000-8000-000000000020' };
              emit({ type: ShellBridgeEventTypeNames.AuthChanged, payload: auth });
            } else {
              emit({
                type: 'host:response',
                requestId: request.requestId,
                ok: false,
                error: { code: 'sign_in_refused', message: 'refused' }
              });
            }
            break;
          }
          case ShellBridgeRequestTypeNames.AuthSignOut:
            reply({});
            auth = { signedIn: false, displayName: null, playerAccountId: null };
            emit({ type: ShellBridgeEventTypeNames.AuthChanged, payload: auth });
            break;
          default:
            // Запуск игры, вызов администратора, язык — учебный хост со всем соглашается.
            reply({});
        }
      },
      addEventListener: (_type, listener) => listeners.add(listener),
      removeEventListener: (_type, listener) => listeners.delete(listener)
    }
  };

  // «Подошли к ПК» — через полсекунды после загрузки, как если бы тронули мышь.
  if (scenario === 'approach') {
    setTimeout(() => emit({ type: ShellBridgeEventTypeNames.InputActivity, payload: {} }), 500);
  }
}
