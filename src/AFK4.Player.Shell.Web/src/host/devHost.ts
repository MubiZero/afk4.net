import {
  PlayerShellStateNames,
  ShellBridgeEventTypeNames,
  ShellBridgeRequestTypeNames,
  type PlayerShellStateDto,
  type ShellAuthStateDto,
  type PlayerExtendOffersDto,
  type PlayerStartOffersDto,
  type ShellSnapshotDto,
  type ShellSystemStateDto
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
  // ?signedIn=1 — сразу вошедший владелец сессии: экраны с деньгами видно без формы входа.
  let auth: ShellAuthStateDto = params.get('signedIn') === '1'
    ? { signedIn: true, displayName: 'Алишер', playerAccountId: '00000000-0000-4000-8000-000000000020' }
    : { signedIn: false, displayName: null, playerAccountId: null };
  let system: ShellSystemStateDto = { volume: 60, micMuted: false, layout: 'RU' };

  window.chrome = {
    webview: {
      postMessage(message: unknown) {
        const request = message as { type?: string; requestId?: string };
        if (!request?.requestId) return;
        const reply = (payload: unknown) => emit({ type: 'host:response', requestId: request.requestId, ok: true, payload });
        switch (request.type) {
          case ShellBridgeRequestTypeNames.ShellReady:
            reply({ state: devScenarioState(scenario), auth, system } satisfies ShellSnapshotDto);
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
          case ShellBridgeRequestTypeNames.SystemSetVolume:
          case ShellBridgeRequestTypeNames.SystemSetMicMuted:
          case ShellBridgeRequestTypeNames.SystemSetLayout:
            // Учебный хост помнит звук и раскладку, как запомнила бы Windows.
            system = { ...system, ...((message as { payload?: Partial<ShellSystemStateDto> }).payload ?? {}) };
            reply(system);
            emit({ type: ShellBridgeEventTypeNames.SystemChanged, payload: system });
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

  installDevApi((next) => emit({ type: ShellBridgeEventTypeNames.StateChanged, payload: devScenarioState(next) }));

  // «Подошли к ПК» — через полсекунды после загрузки, как если бы тронули мышь.
  if (scenario === 'approach') {
    setTimeout(() => emit({ type: ShellBridgeEventTypeNames.InputActivity, payload: {} }), 500);
  }
}

/**
 * Учебный сервер клуба: цены для «Сколько играем» и старт. Настоящий адрес из учебного состояния
 * никуда не ведёт, поэтому запросы к нему отвечаются здесь; остальные уходят как есть.
 */
function installDevApi(moveTo: (scenario: DevScenario) => void): void {
  const realFetch = window.fetch.bind(window);
  const devFetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const url = new URL(typeof input === 'string' ? input : input instanceof URL ? input.href : input.url);
    const post = init?.method === 'POST';
    if (url.pathname === '/api/me/this-pc/start-offers') {
      return json(devStartOffers(Date.now()));
    }
    if (url.pathname === '/api/me/sessions/start' && post) {
      setTimeout(() => moveTo('session'), 600);
      return json({});
    }
    if (url.pathname.endsWith('/extend-offers')) {
      return json(devExtendOffers(Date.now()));
    }
    if (url.pathname.endsWith('/extend') && post) {
      return json({});
    }
    if (url.pathname.endsWith('/end-quote')) {
      return json({ billedMinutes: 35, refund: TJS(1_000), packageMinutesReturned: 0 });
    }
    if (url.pathname.endsWith('/end') && post) {
      setTimeout(() => moveTo('idle'), 400);
      return json({ billedMinutes: 35, refunded: TJS(1_000), packageMinutesReturned: 0 });
    }
    return realFetch(input, init);
  };
  // Тип fetch у Bun шире браузерного (preconnect): учебному хосту в браузере нужен только вызов.
  window.fetch = devFetch as typeof fetch;
}

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

const TJS = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });

export function devStartOffers(nowMs: number): PlayerStartOffersDto {
  const balance = 4_500;
  const perHour = 1_000;
  return {
    seatLabel: 'ПК 07',
    zoneName: 'Общий зал',
    timeZone: 'Asia/Dushanbe',
    balance: TJS(balance),
    tariffs: [
      {
        tariffVersionId: '00000000-0000-4000-8000-000000000101',
        tariffRuleVersionId: '00000000-0000-4000-8000-000000000101',
        name: 'Стандарт',
        pricePerHour: TJS(perHour),
        appliesNow: true,
        startsAtUtc: null,
        options: [60, 120, 180, 300].map((minutes) => {
          const amount = (minutes / 60) * perHour;
          return {
            minutes,
            billableMinutes: minutes,
            endsAtUtc: new Date(nowMs + minutes * 60_000).toISOString(),
            amount: TJS(amount),
            balanceAfter: TJS(balance - amount),
            affordable: amount <= balance
          };
        })
      },
      {
        tariffVersionId: '00000000-0000-4000-8000-000000000102',
        tariffRuleVersionId: '00000000-0000-4000-8000-000000000102',
        name: 'Ночь',
        pricePerHour: TJS(600),
        appliesNow: false,
        startsAtUtc: new Date(nowMs + 3 * 3_600_000).toISOString(),
        options: []
      }
    ],
    packages: [
      { playerPackageId: '00000000-0000-4000-8000-000000000201', name: 'Пакет «5 часов»', remainingMinutes: 200, expiresAtUtc: null }
    ]
  };
}

export function devExtendOffers(nowMs: number): PlayerExtendOffersDto {
  const balance = 4_500;
  const endsAt = nowMs + 95 * 60_000;
  return {
    sessionId: '00000000-0000-4000-8000-000000000010',
    balance: TJS(balance),
    options: [30, 60, 120, 180].map((minutes) => {
      const amount = (minutes / 60) * 1_000;
      return {
        minutes,
        billableMinutes: minutes,
        endsAtUtc: new Date(endsAt + minutes * 60_000).toISOString(),
        amount: TJS(amount),
        balanceAfter: TJS(balance - amount),
        affordable: amount <= balance
      };
    }),
    unavailableReason: null
  };
}
