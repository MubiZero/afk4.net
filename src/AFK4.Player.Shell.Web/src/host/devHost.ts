import {
  PlayerShellStateNames,
  ShellBridgeEventTypeNames,
  ShellBridgeRequestTypeNames,
  type PlayerShellStateDto,
  type ShellAuthStateDto,
  type PlayerExtendOffersDto,
  type PlayerStartOffersDto,
  type ShopCatalogItemDto,
  type ShopOrderDto,
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

// Картинка-заглушка для учебного стенда: настоящие лежат в кэше ПК, которого у браузера нет.
const DEV_PHOTO = `data:image/svg+xml,${encodeURIComponent(
  '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1600 900"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1">'
  + '<stop offset="0" stop-color="#1b2a4a"/><stop offset=".55" stop-color="#3b1f5c"/><stop offset="1" stop-color="#0c6b58"/></linearGradient></defs>'
  + '<rect width="1600" height="900" fill="url(#g)"/><circle cx="1180" cy="380" r="260" fill="#f2c14e" opacity=".18"/>'
  + '<circle cx="1320" cy="620" r="180" fill="#2cc592" opacity=".22"/></svg>'
)}`;

const DEV_SHOWCASE = (minutes: (count: number) => string): PlayerShellStateDto['showcase'] => [
  { cardId: 'news:dev', kind: 'news', title: 'Ночь CS2 в пятницу', body: 'С 22:00 до утра — турнир на пять команд, призы от клуба и пицца в перерывах.', imageUrl: DEV_PHOTO },
  { cardId: 'tariff:dev', kind: 'tariff', title: 'Ночной', price: { currencyCode: 'TJS', minorUnits: 600 }, timeWindow: '22:00–06:00' },
  // Реклама платформы — каждой третьей, как у клуба на бесплатном тарифе.
  { cardId: 'ad:dev', kind: 'ad', title: 'Безлимит на месяц', body: 'Интернет для игр без ограничений по трафику.', advertiser: 'Сомон Телеком' },
  { cardId: 'tournament:dev', kind: 'tournament', title: 'Кубок зала', subtitle: 'Dota 2', startsAtUtc: minutes(60 * 50) },
  {
    cardId: 'packages:dev', kind: 'packages', title: '',
    packages: [
      { name: '3 часа', price: { currencyCode: 'TJS', minorUnits: 2500 }, minutes: 180 },
      { name: '5 часов', price: { currencyCode: 'TJS', minorUnits: 4000 }, minutes: 300 },
      { name: 'Ночь', price: { currencyCode: 'TJS', minorUnits: 5000 }, minutes: 480 }
    ]
  },
  { cardId: 'bar_hit:dev', kind: 'bar_hit', title: 'Кола 0,5', price: { currencyCode: 'TJS', minorUnits: 1000 } }
];

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
    features: ['player_shop', 'loyalty', 'online_topup'],
    showcase: DEV_SHOWCASE(minutes)
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
      return {
        ...base,
        state: PlayerShellStateNames.Maintenance,
        seatingCode: null,
        seatingCodeExpiresAtUtc: null,
        maintenanceSinceUtc: minutes(-35),
        maintenanceByName: 'Шерзод'
      };
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
          case ShellBridgeRequestTypeNames.MaintenanceReturn:
            // «Вернуть в зал»: агент закрыл бы рабочий стол, а сервер прислал бы «Свободен».
            reply({});
            emit({ type: ShellBridgeEventTypeNames.StateChanged, payload: devScenarioState('idle') });
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
    if (url.pathname.startsWith('/api/me/visits/') && url.pathname.endsWith('/receipt')) {
      const ended = Date.now();
      return json({
        receiptNumber: 'S-000142', createdAtUtc: new Date(ended).toISOString(),
        sessionId: url.pathname.split('/')[4], seatName: 'ПК 07',
        startedAtUtc: new Date(ended - 95 * 60_000).toISOString(), endedAtUtc: new Date(ended).toISOString(),
        timeChargeMinorUnits: 1_600, posLines: [], posTotalMinorUnits: 2_400, grandTotalMinorUnits: 4_000, currencyCode: 'TJS'
      });
    }
    if (url.pathname === '/api/me/reviews' && post) {
      return json({ rating: 5, count: 1, reviews: [] });
    }
    if (url.pathname === '/api/me/dashboard') {
      return json({ walletBalance: TJS(devBalance), heldBalance: TJS(0), debtBalance: TJS(0), activeSession: null });
    }
    if (url.pathname === '/api/me/wallet/top-up-methods') {
      return json({ counter: true, online: true });
    }
    if (url.pathname === '/api/me/wallet/top-up-intent' && post) {
      const { amountMinorUnits } = JSON.parse(String(init?.body ?? '{}')) as { amountMinorUnits: number };
      devTopUp = { amount: amountMinorUnits, asked: 0 };
      return json({
        paymentIntentId: crypto.randomUUID(), amountMinorUnits, currencyCode: 'TJS', state: 'pending', purpose: 'top_up',
        method: 'eskhata', createdAtUtc: new Date().toISOString(), fulfilledAtUtc: null, isExpired: false,
        qr: 'https://pay.example.test/afk4-dev-top-up'
      });
    }
    if (url.pathname.endsWith('/eskhata-status') && post) {
      // Учебный банк отвечает «оплачено» на третий вопрос — будто человек дошёл до кнопки в приложении.
      if (devTopUp && ++devTopUp.asked >= 3) {
        devBalance += devTopUp.amount;
        devTopUp = null;
        return json({ payment: 'paid' });
      }
      return json({ payment: 'pending' });
    }
    if (url.pathname === '/api/me/shop/catalog') {
      return json(DEV_CATALOG);
    }
    if (url.pathname === '/api/me/shop/orders' && post) {
      const lines = (JSON.parse(String(init?.body ?? '{}')) as { lines: { productId: string; quantity: number }[] }).lines;
      const order = devOrder(lines);
      devOrders = [order, ...devOrders];
      // Стойка приняла заказ через несколько секунд — как настоящая.
      setTimeout(() => {
        devOrders = devOrders.map((existing) => (existing.id === order.id ? { ...existing, status: 'accepted' } : existing));
      }, 8_000);
      return json(order);
    }
    if (url.pathname === '/api/me/shop/orders') {
      return json(devOrders);
    }
    if (url.pathname.startsWith('/api/me/shop/orders/') && url.pathname.endsWith('/cancel') && post) {
      const id = url.pathname.split('/')[5];
      devOrders = devOrders.map((existing) => (existing.id === id ? { ...existing, status: 'cancelled' } : existing));
      return json(devOrders.find((existing) => existing.id === id));
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

const DEV_CATALOG: ShopCatalogItemDto[] = [
  { productId: '00000000-0000-4000-8000-000000000301', name: 'Кола 0,5 л', sku: 'COLA05', price: TJS(1_200), stockOnHand: 24 },
  { productId: '00000000-0000-4000-8000-000000000302', name: 'Энергетик', sku: 'ENERGY', price: TJS(1_800), stockOnHand: 2 },
  { productId: '00000000-0000-4000-8000-000000000303', name: 'Чипсы', sku: 'CHIPS', price: TJS(900), stockOnHand: 0 },
  { productId: '00000000-0000-4000-8000-000000000304', name: 'Лаваш с курицей', sku: 'LAVASH', price: TJS(2_500), stockOnHand: 0 }
];

let devOrders: ShopOrderDto[] = [];

function devOrder(lines: { productId: string; quantity: number }[]): ShopOrderDto {
  const orderLines = lines.map((line) => {
    const item = DEV_CATALOG.find((candidate) => candidate.productId === line.productId)!;
    return { productId: item.productId, name: item.name, unitPrice: item.price, quantity: line.quantity, lineTotal: TJS(item.price.minorUnits * line.quantity) };
  });
  return {
    id: crypto.randomUUID(),
    branchId: '00000000-0000-4000-8000-000000000002',
    seatId: '00000000-0000-4000-8000-000000000004',
    playerAccountId: '00000000-0000-4000-8000-000000000020',
    playerDisplayName: 'Алишер',
    status: 'placed',
    total: TJS(orderLines.reduce((sum, line) => sum + line.lineTotal.minorUnits, 0)),
    lines: orderLines,
    placedAtUtc: new Date().toISOString(),
    acceptedAtUtc: null,
    deliveredAtUtc: null,
    cancelledAtUtc: null,
    version: 1,
    posSaleId: null,
    seatName: 'ПК 07'
  };
}

let devBalance = 4_500;
let devTopUp: { amount: number; asked: number } | null = null;
