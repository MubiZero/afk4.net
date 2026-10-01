import {
  PlayerShellStateNames,
  ShellBridgeEventTypeNames,
  ShellBridgeRequestTypeNames,
  ShopOrderStatusNames,
  type PlayerShellStateDto,
  type ShellAuthStateDto,
  type PlayerExtendOffersDto,
  type LaunchedAppDto,
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
 * Только для dev-сборки и публичного демо (main.tsx подключает его под `import.meta.env.DEV` или
 * `VITE_AFK4_DEMO=1`, Vite вырезает ветку из боевой сборки; это проверяет
 * demo/prodBundle.test.ts). Образец — devHostBridge Панели. Сервер клуба тоже учебный и живёт в
 * памяти страницы: баланс, заказы бара и сессия меняются от действий посетителя, наружу не уходит
 * ничего.
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

export const DEV_SCENARIOS: readonly DevScenario[] = ['idle', 'approach', 'session', 'ending', 'grace', 'offline', 'maintenance', 'error', 'connecting'];

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
  // Реклама по закону: таджикский первым, русский ниже, пометки продавца, сертификации и срока.
  {
    cardId: 'ad:dev', kind: 'ad', title: 'Интернети бемаҳдуд барои як моҳ', body: 'Интернет барои бозӣ бе маҳдудияти трафик.',
    secondaryTitle: 'Безлимит на месяц', secondaryBody: 'Интернет для игр без ограничений по трафику.', advertiser: 'Сомон Телеком',
    seller: { legalName: 'ООО «Сомон Телеком»', taxId: '123456789', address: 'Душанбе, пр. Рудаки 1' },
    offerUntilUtc: '2026-10-31T00:00:00Z'
  },
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

/** Сколько минут до конца у сценариев с идущей сессией, если сессию не начинали и не продлевали. */
const DEFAULT_SESSION_MINUTES: Partial<Record<DevScenario, number>> = { session: 95, ending: 0.8, grace: 12 };

export function devScenarioState(scenario: DevScenario, nowMs = Date.now(), remainingMinutes?: number): PlayerShellStateDto | null {
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
    // Аренда — 15 минут, как у сервера; отсчёт идёт от конца сессии. Раньше практика клала конец
    // сессии в срок аренды — и баг отсчёта от аренды в браузере был не виден.
    leaseExpiresAtUtc: minutes(Math.min(15, untilMinutes)),
    remainingSeconds: Math.round(untilMinutes * 60),
    sessionStartedAtUtc: minutes(-40),
    sessionEndsAtUtc: minutes(untilMinutes),
    seatingCode: null,
    seatingCodeExpiresAtUtc: null,
    sessionOwnerKind: 'player',
    sessionOwnerPlayerAccountId: '00000000-0000-4000-8000-000000000020',
    launchedApps: [{ launchId: '00000000-0000-4000-8000-000000000030', appId: 'cs2', displayName: 'Counter-Strike 2', processIds: [4242] }]
  });

  switch (scenario) {
    case 'session':
      return playing(remainingMinutes ?? 95, PlayerShellStateNames.Active);
    case 'ending':
      return { ...playing(remainingMinutes ?? 0.8, PlayerShellStateNames.Ending), warningKind: 'low_time' };
    case 'grace':
      return {
        ...playing(remainingMinutes ?? 12, PlayerShellStateNames.Grace),
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

/** Что учебный хост даёт странице-обёртке: переключатель сценариев в полосе «Демо». */
export interface DevHostControl {
  getScenario(): DevScenario;
  subscribe(listener: () => void): () => void;
  /** Перейти к сценарию, как если бы ПК оказался в таком состоянии: вход игрока и конец сессии подстраиваются. */
  setScenario(next: DevScenario): void;
  /** Человек тронул мышь или клавиатуру за экраном: у настоящего ПК об этом сообщает агент. */
  touch(): void;
}

/** Сколько тишины должно пройти, чтобы ввод считался «подошли снова», а не продолжением движения мыши. */
const INPUT_QUIET_MS = 4_000;

const ALISHER: ShellAuthStateDto = { signedIn: true, displayName: 'Алишер', playerAccountId: '00000000-0000-4000-8000-000000000020' };
const SIGNED_OUT: ShellAuthStateDto = { signedIn: false, displayName: null, playerAccountId: null };

export function installDevHost(): DevHostControl {
  const params = new URLSearchParams(window.location.search);
  const requested = params.get('scenario') as DevScenario | null;
  let scenario: DevScenario = requested && DEV_SCENARIOS.includes(requested) ? requested : 'idle';
  const listeners = new Set<(event: HostBridgeMessageEvent) => void>();
  const watchers = new Set<() => void>();
  const emit = (data: unknown) => queueMicrotask(() => {
    for (const listener of listeners) listener({ data });
  });
  // ?signedIn=1 — сразу вошедший владелец сессии: экраны с деньгами видно без формы входа. У сценариев
  // с идущей сессией он вошёл всегда: сессия на его счёте, и без входа продлить и заказать нельзя.
  let auth: ShellAuthStateDto = params.get('signedIn') === '1' || scenario in DEFAULT_SESSION_MINUTES ? ALISHER : SIGNED_OUT;
  let system: ShellSystemStateDto = { volume: 60, micMuted: false, layout: 'RU' };
  const setAuth = (next: ShellAuthStateDto) => {
    auth = next;
    emit({ type: ShellBridgeEventTypeNames.AuthChanged, payload: auth });
  };

  // Идущая сессия в памяти: конец сдвигают продление и старт, запущенные игры — запуск и закрытие.
  const inSession = () => scenario in DEFAULT_SESSION_MINUTES;
  let sessionEndsAtMs = Date.now() + (DEFAULT_SESSION_MINUTES[scenario] ?? 0) * 60_000;
  let launched: LaunchedAppDto[] = devScenarioState('session')?.launchedApps ?? [];
  let expiry: number | undefined;

  const publish = () => {
    window.clearTimeout(expiry);
    const now = Date.now();
    const state = devScenarioState(scenario, now, inSession() ? Math.max(0, (sessionEndsAtMs - now) / 60_000) : undefined);
    emit({ type: ShellBridgeEventTypeNames.StateChanged, payload: state?.launchedApps ? { ...state, launchedApps: launched } : state });
    // Время вышло — сервер запер бы ПК: игрок увидит итог, а не нулевой отсчёт.
    if (inSession()) {
      expiry = window.setTimeout(() => moveTo('idle'), Math.max(0, sessionEndsAtMs - now) + 1_500);
    }
    for (const watcher of watchers) watcher();
  };
  const moveTo = (next: DevScenario) => {
    scenario = next;
    publish();
  };

  const beginSession = (minutes: number) => {
    sessionEndsAtMs = Date.now() + minutes * 60_000;
    launched = [];
    moveTo('session');
  };
  const extendSession = (minutes: number) => {
    sessionEndsAtMs += minutes * 60_000;
    // Продлили на исходе времени — «Время вышло» снимается, как у сервера.
    moveTo(scenario === 'ending' ? 'session' : scenario);
  };

  window.chrome = {
    webview: {
      postMessage(message: unknown) {
        const request = message as { type?: string; requestId?: string };
        if (!request?.requestId) return;
        const payload = (message as { payload?: Record<string, unknown> }).payload ?? {};
        const reply = (body: unknown) => emit({ type: 'host:response', requestId: request.requestId, ok: true, payload: body });
        switch (request.type) {
          case ShellBridgeRequestTypeNames.ShellReady:
            reply({ state: devScenarioState(scenario, Date.now(), inSession() ? Math.max(0, (sessionEndsAtMs - Date.now()) / 60_000) : undefined), auth, system } satisfies ShellSnapshotDto);
            break;
          case ShellBridgeRequestTypeNames.AuthSignIn: {
            // Учебный вход: ПИН-код 123456 пускает, остальные — нет, как ответил бы сервер.
            if (payload.pin === '123456') {
              reply({});
              setAuth(ALISHER);
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
          case ShellBridgeRequestTypeNames.AuthRefresh:
            // Учебный вход не истекает: обновлять нечего, игрок остаётся.
            reply(auth);
            break;
          case ShellBridgeRequestTypeNames.AuthSignOut:
            reply({});
            setAuth(SIGNED_OUT);
            break;
          case ShellBridgeRequestTypeNames.SystemSetVolume:
          case ShellBridgeRequestTypeNames.SystemSetMicMuted:
          case ShellBridgeRequestTypeNames.SystemSetLayout:
            // Учебный хост помнит звук и раскладку, как запомнила бы Windows.
            system = { ...system, ...(payload as Partial<ShellSystemStateDto>) };
            reply(system);
            emit({ type: ShellBridgeEventTypeNames.SystemChanged, payload: system });
            break;
          case ShellBridgeRequestTypeNames.MaintenanceReturn:
            // «Вернуть в зал»: агент закрыл бы рабочий стол, а сервер прислал бы «Свободен».
            reply({});
            moveTo('idle');
            break;
          case ShellBridgeRequestTypeNames.AppLaunch: {
            // Игра запустилась: агент прислал бы состояние с ней в списке «Мои приложения».
            const app = devScenarioState('session')?.launcherApps?.find((candidate) => candidate.appId === payload.appId);
            reply({});
            if (app && inSession()) {
              launched = [...launched, { launchId: crypto.randomUUID(), appId: app.appId, displayName: app.displayName, processIds: [4300 + launched.length] }];
              publish();
            }
            break;
          }
          case ShellBridgeRequestTypeNames.AppClose:
            reply({});
            launched = launched.filter((app) => app.launchId !== payload.launchId);
            publish();
            break;
          default:
            // Вернуться в игру, вызов администратора, язык — учебный хост со всем соглашается.
            reply({});
        }
      },
      addEventListener: (_type, listener) => listeners.add(listener),
      removeEventListener: (_type, listener) => listeners.delete(listener)
    }
  };

  installDevApi({ beginSession, extendSession, endSession: () => moveTo('idle'), sessionEndsAtMs: () => sessionEndsAtMs });

  // «Подошли к ПК» — через полсекунды после загрузки, как если бы тронули мышь.
  if (scenario === 'approach') {
    setTimeout(() => emit({ type: ShellBridgeEventTypeNames.InputActivity, payload: {} }), 500);
  }
  if (inSession()) publish();

  let lastInputMs = 0;
  return {
    getScenario: () => scenario,
    touch() {
      const now = Date.now();
      const quiet = now - lastInputMs > INPUT_QUIET_MS;
      lastInputMs = now;
      if (quiet && (scenario === 'idle' || scenario === 'approach')) emit({ type: ShellBridgeEventTypeNames.InputActivity, payload: {} });
    },
    subscribe: (listener) => {
      watchers.add(listener);
      return () => watchers.delete(listener);
    },
    setScenario(next) {
      // Сценарий — всегда «с чистого места»: сессия идёт с полным временем, а вход игрока — только у сессии.
      sessionEndsAtMs = Date.now() + (DEFAULT_SESSION_MINUTES[next] ?? 0) * 60_000;
      launched = devScenarioState('session')?.launchedApps ?? [];
      const wantsSignIn = next in DEFAULT_SESSION_MINUTES;
      if (auth.signedIn !== wantsSignIn) setAuth(wantsSignIn ? ALISHER : SIGNED_OUT);
      moveTo(next);
      // Подошёл или отошёл — сигнал хоста, по которому экран открывает окно входа или возвращает витрину.
      emit({ type: next === 'approach' ? ShellBridgeEventTypeNames.InputActivity : ShellBridgeEventTypeNames.InputIdle, payload: {} });
    }
  };
}

interface DevSessionControl {
  beginSession(minutes: number): void;
  extendSession(minutes: number): void;
  endSession(): void;
  sessionEndsAtMs(): number;
}

/**
 * Учебный сервер клуба: цены, старт, продление, бар, пополнение и чаевые отвечают здесь же, в памяти
 * страницы, и держат один баланс на всех. Настоящий адрес из учебного состояния никуда не ведёт, а
 * наружу не уходит ничего: чужой адрес получает отказ, а не запрос в сеть.
 */
function installDevApi(session: DevSessionControl): void {
  const realFetch = window.fetch.bind(window);
  const devFetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const url = new URL(typeof input === 'string' ? input : input instanceof URL ? input.href : input.url, window.location.href);
    const post = init?.method === 'POST';
    const body = () => JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>;
    if (url.pathname === '/api/me/this-pc/start-offers') {
      return json(devStartOffers(Date.now(), devBalance, devPackageMinutes));
    }
    if (url.pathname === '/api/me/sessions/start' && post) {
      const { tariffRuleVersionId, durationMinutes, playerPackageId } = body() as { tariffRuleVersionId: string; durationMinutes: number; playerPackageId: string | null };
      if (playerPackageId) {
        if (durationMinutes > devPackageMinutes) return failure(409, 'insufficient_balance');
        devPackageMinutes -= durationMinutes;
      } else {
        const tariff = devStartOffers(Date.now()).tariffs.find((candidate) => candidate.tariffRuleVersionId === tariffRuleVersionId);
        if (!tariff || !tariff.appliesNow) return failure(409, 'tariff_outside_its_hours');
        const amount = (durationMinutes / 60) * tariff.pricePerHour.minorUnits;
        if (amount > devBalance) return failure(409, 'insufficient_balance');
        devBalance -= amount;
      }
      setTimeout(() => session.beginSession(durationMinutes), 600);
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
    if (url.pathname.startsWith('/api/me/visits/') && url.pathname.endsWith('/tip')) {
      if (!post) {
        return json({
          available: true, unavailableReason: null, presets: [TJS(500), TJS(1_000), TJS(2_000)], balance: TJS(devBalance),
          recipientName: 'Шерзод', given: null
        });
      }
      const amount = (body().amount as { minorUnits: number }).minorUnits;
      if (amount > devBalance) return failure(409, 'insufficient_balance');
      devBalance -= amount;
      return json({ amount: TJS(amount), balanceAfter: TJS(devBalance), recipientName: 'Шерзод' });
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
      const { amountMinorUnits } = body() as { amountMinorUnits: number };
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
      const order = devOrder((body() as { lines: { productId: string; quantity: number }[] }).lines);
      if (order.total.minorUnits > devBalance) return failure(409, 'insufficient_funds');
      devBalance -= order.total.minorUnits;
      devOrders = [order, ...devOrders];
      // Стойка приняла заказ через несколько секунд, а потом принесли — как настоящая.
      const advance = (status: ShopOrderDto['status']) => {
        devOrders = devOrders.map((existing) => (existing.id === order.id && existing.status !== ShopOrderStatusNames.Cancelled ? { ...existing, status } : existing));
      };
      setTimeout(() => {
        advance(ShopOrderStatusNames.Accepted);
        setTimeout(() => advance(ShopOrderStatusNames.Delivered), 12_000);
      }, 8_000);
      return json(order);
    }
    if (url.pathname === '/api/me/shop/orders') {
      return json(devOrders);
    }
    if (url.pathname.startsWith('/api/me/shop/orders/') && url.pathname.endsWith('/cancel') && post) {
      const id = url.pathname.split('/')[5];
      const order = devOrders.find((existing) => existing.id === id);
      // Заказ, который уже готовят, отменить нельзя — как ответит стойка.
      if (!order || order.status !== ShopOrderStatusNames.Placed) return failure(409, 'order_not_cancellable');
      devBalance += order.total.minorUnits;
      devOrders = devOrders.map((existing) => (existing.id === id ? { ...existing, status: ShopOrderStatusNames.Cancelled } : existing));
      return json(devOrders.find((existing) => existing.id === id));
    }
    if (url.pathname.endsWith('/extend-offers')) {
      return json(devExtendOffers(Date.now(), devBalance, session.sessionEndsAtMs()));
    }
    if (url.pathname.endsWith('/extend') && post) {
      const minutes = body().additionalMinutes as number;
      const amount = (minutes / 60) * 1_000;
      if (amount > devBalance) return failure(409, 'insufficient_balance');
      devBalance -= amount;
      session.extendSession(minutes);
      return json({});
    }
    if (url.pathname.endsWith('/end-quote')) {
      return json({ billedMinutes: 35, refund: TJS(1_000), packageMinutesReturned: 0 });
    }
    if (url.pathname.endsWith('/end') && post) {
      devBalance += 1_000;
      setTimeout(() => session.endSession(), 400);
      return json({ billedMinutes: 35, refunded: TJS(1_000), packageMinutesReturned: 0 });
    }
    // Только своя страница: чужой адрес — отказ, а не запрос в сеть.
    return url.origin === window.location.origin ? realFetch(input, init) : failure(404, 'dev_host_has_no_route');
  };
  // Тип fetch у Bun шире браузерного (preconnect): учебному хосту в браузере нужен только вызов.
  window.fetch = devFetch as typeof fetch;
}

function failure(status: number, code: string): Response {
  return new Response(JSON.stringify({ error: code }), { status, headers: { 'Content-Type': 'application/json' } });
}

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

const TJS = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });

export function devStartOffers(nowMs: number, balance = 4_500, packageMinutes = 200): PlayerStartOffersDto {
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
    packages: packageMinutes <= 0 ? [] : [
      { playerPackageId: '00000000-0000-4000-8000-000000000201', name: 'Пакет «5 часов»', remainingMinutes: packageMinutes, expiresAtUtc: null }
    ]
  };
}

export function devExtendOffers(nowMs: number, balance = 4_500, endsAt = nowMs + 95 * 60_000): PlayerExtendOffersDto {
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
    status: ShopOrderStatusNames.Placed,
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
let devPackageMinutes = 200;
let devTopUp: { amount: number; asked: number } | null = null;
