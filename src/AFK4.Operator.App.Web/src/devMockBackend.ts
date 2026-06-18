// DEV-ONLY mock backend for the browser UI preview. Lets the operator console render with
// representative data WITHOUT a real platform API, so the UI/UX (including the light/dark theme)
// can be reviewed in a plain `bun run dev` browser run. Imported only from devHostBridge, which is
// itself gated on import.meta.env.DEV — so this never ships in the production MSI bundle.
//
// Fixtures mirror the shapes the test suite already exercises. Unmapped endpoints fall back to an
// empty list, so secondary screens render their (themed) empty/error states rather than crashing.
import { permissionNames } from './operatorPermissions';

const ORG = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';
const BRANCH = 'acfc0212-967f-4d84-94be-9003387b09c2';
const FAR_FUTURE = '2099-01-01T00:00:00Z';

export function createMockSession(): Record<string, unknown> {
  return {
    staffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
    organizationId: ORG,
    displayName: 'Оператор смены',
    accessToken: 'preview-access-token',
    accessTokenExpiresAtUtc: FAR_FUTURE,
    refreshTokenExpiresAtUtc: FAR_FUTURE,
    branchIds: [BRANCH],
    activeBranchId: BRANCH,
    permissions: Object.values(permissionNames)
  };
}

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

function noContent(): Response {
  return new Response(null, { status: 204 });
}

const money = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });

function floorMap() {
  return {
    branchId: BRANCH,
    branchName: 'AFK4 Dushanbe',
    seats: [
      // Зал A — рабочий зал: смесь живых сессий, свободных и одного «ПК офлайн» (сессия идёт, связь потеряна).
      { seatId: 'a1', seatName: 'PC-01', zoneId: 'z-a', zoneName: 'Зал A', sortOrder: 10, state: 'Active', deviceId: 'd1', deviceName: 'PC-01', isDeviceOnline: true, isDeviceLocked: false, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: 's1', remainingSeconds: 2580, playerDisplayName: 'Амир К.', tariffName: 'Стандарт', sessionStartedAtUtc: minutesAgoUtc(75) },
      { seatId: 'a2', seatName: 'PC-02', zoneId: 'z-a', zoneName: 'Зал A', sortOrder: 20, state: 'Free', deviceId: 'd2', deviceName: 'PC-02', isDeviceOnline: true, isDeviceLocked: true, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: null, remainingSeconds: null },
      { seatId: 'a3', seatName: 'PC-03', zoneId: 'z-a', zoneName: 'Зал A', sortOrder: 30, state: 'Active', deviceId: 'd3', deviceName: 'PC-03', isDeviceOnline: true, isDeviceLocked: false, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: 's3', remainingSeconds: null, accruedCostMinorUnits: 5400, currencyCode: 'TJS', playerDisplayName: 'Юсуф А.', tariffName: 'Почасовой', sessionStartedAtUtc: minutesAgoUtc(110) },
      { seatId: 'a4', seatName: 'PC-04', zoneId: 'z-a', zoneName: 'Зал A', sortOrder: 40, state: 'Active', deviceId: 'd4', deviceName: 'PC-04', isDeviceOnline: false, isDeviceLocked: true, lastHeartbeatAtUtc: '2026-05-21T09:50:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: 's4', remainingSeconds: 1200 },
      { seatId: 'a5', seatName: 'PC-05', zoneId: 'z-a', zoneName: 'Зал A', sortOrder: 50, state: 'Requested', deviceId: 'd7', deviceName: 'PC-05', isDeviceOnline: true, isDeviceLocked: true, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: null, remainingSeconds: null },
      { seatId: 'a6', seatName: 'PC-06', zoneId: 'z-a', zoneName: 'Зал A', sortOrder: 60, state: 'Free', deviceId: 'd8', deviceName: 'PC-06', isDeviceOnline: true, isDeviceLocked: true, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: null, remainingSeconds: null },
      // VIP — поменьше, с одним местом на обслуживании.
      { seatId: 'b1', seatName: 'VIP-01', zoneId: 'z-vip', zoneName: 'VIP', sortOrder: 10, state: 'Active', deviceId: 'd5', deviceName: 'VIP-01', isDeviceOnline: true, isDeviceLocked: false, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: 's5', remainingSeconds: 5400, playerDisplayName: 'Мадина С.', tariffName: 'VIP час', sessionStartedAtUtc: minutesAgoUtc(50) },
      { seatId: 'b2', seatName: 'VIP-02', zoneId: 'z-vip', zoneName: 'VIP', sortOrder: 20, state: 'Maintenance', deviceId: 'd6', deviceName: 'VIP-02', isDeviceOnline: false, isDeviceLocked: false, lastHeartbeatAtUtc: '2026-05-21T08:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: null, remainingSeconds: null },
      { seatId: 'b3', seatName: 'VIP-03', zoneId: 'z-vip', zoneName: 'VIP', sortOrder: 30, state: 'Free', deviceId: 'd9', deviceName: 'VIP-03', isDeviceOnline: true, isDeviceLocked: true, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: null, remainingSeconds: null },
      // Зал B — две проблемы (ошибка команды + нет связи) и одна сессия.
      { seatId: 'c1', seatName: 'PC-07', zoneId: 'z-b', zoneName: 'Зал B', sortOrder: 10, state: 'Failed', deviceId: 'd10', deviceName: 'PC-07', isDeviceOnline: true, isDeviceLocked: false, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: null, remainingSeconds: null },
      { seatId: 'c2', seatName: 'PC-08', zoneId: 'z-b', zoneName: 'Зал B', sortOrder: 20, state: 'Offline', deviceId: 'd11', deviceName: 'PC-08', isDeviceOnline: false, isDeviceLocked: true, lastHeartbeatAtUtc: '2026-05-21T07:30:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: null, remainingSeconds: null },
      { seatId: 'c3', seatName: 'PC-09', zoneId: 'z-b', zoneName: 'Зал B', sortOrder: 30, state: 'Active', deviceId: 'd12', deviceName: 'PC-09', isDeviceOnline: true, isDeviceLocked: false, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: 's9', remainingSeconds: 900 }
    ]
  };
}

function dashboardSummary() {
  return {
    organizationId: ORG, branchId: BRANCH,
    fromUtc: '2026-05-21T00:00:00Z', toUtc: '2026-05-21T23:59:59Z', generatedAtUtc: '2026-05-21T12:00:00Z',
    shift: { shiftId: 'sh1', state: 'open', openedAtUtc: '2026-05-21T08:00:00Z', openedByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134', expectedCash: money(112000) },
    revenue: { posNetSales: money(120000), gameplayRevenue: money(480000), totalRevenue: money(600000), posCheckCount: 18, newPlayerCount: 4 },
    utilization: { totalSeats: 6, activeSessions: 3, endingSessions: 1, onlineDevices: 4, offlineDevices: 2, sessionStarts: 12, utilizationPercent: 50 },
    alertPressure: { pendingCommands: 1, failedCommands: 1, offlineDevices: 2, endingSessions: 1, totalAlerts: 5 },
    reservations: { activeReservations: 2, availableSlots: 3, source: 'floor-map-availability' },
    focusQueue: [
      { tone: 'blocking', target: 'PC-04', title: 'lock Failed', detail: 'Agent did not confirm lock.', seatId: 'a4', deviceId: 'd4', createdAtUtc: '2026-05-21T10:00:00Z', sourceType: 'device-command' },
      { tone: 'warning', target: 'PC-03', title: 'Сессия заканчивается', detail: 'Осталось 4 минуты.', seatId: 'a3', deviceId: 'd3', createdAtUtc: '2026-05-21T10:05:00Z', sourceType: 'session' }
    ],
    recentPayments: [
      { paymentId: 'p1', posSaleId: 'ps1', shiftId: 'sh1', createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134', paymentKind: 'payment', paymentMethod: 'cash', amount: money(1200), createdAtUtc: '2026-05-21T09:01:00Z' }
    ]
  };
}

function currentShift() {
  return {
    shiftId: 'sh1', organizationId: ORG, branchId: BRANCH,
    openedByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134', closedByStaffUserId: null, state: 'open',
    startingCash: money(100000), countedCash: null, expectedCash: money(112000), difference: money(0),
    openingNote: 'Дневная смена', closingNote: '', openedAtUtc: '2026-05-21T08:00:00Z', closedAtUtc: null
  };
}

function posProduct() {
  return {
    productId: 'prod1', organizationId: ORG, branchId: BRANCH, categoryId: 'cat1',
    name: 'Cola 0.5', sku: 'COLA-05', price: money(1200), trackStock: true, allowNegativeStock: false,
    isActive: true, stockOnHand: 12, createdAtUtc: '2026-05-21T08:00:00Z'
  };
}

// Брони привязываем к «сегодня» (локальный день оператора): таймлайн строится вокруг текущей
// даты, поэтому фикстуры на статичную дату оставляли бы превью брони вечно пустым.
function todayAtUtc(hour: number, minute = 0): string {
  const d = new Date();
  d.setHours(hour, minute, 0, 0);
  return d.toISOString();
}

// Старт сессии «N минут назад» от текущего момента — чтобы в превью полоса сессии всегда начиналась
// до now-линии независимо от времени просмотра.
function minutesAgoUtc(minutes: number): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

function booking(
  id: string,
  startHour: number,
  durationMinutes: number,
  state: string,
  source: string,
  customerName: string,
  phoneNumber: string,
  seat: { id: string; name: string; zone: string } | null,
  note: string
) {
  return {
    reservationId: id, organizationId: ORG, branchId: BRANCH, playerAccountId: null,
    seatId: seat?.id ?? '', seatName: seat?.name ?? '', zoneName: seat?.zone ?? '',
    customerName, phoneNumber,
    startsAtUtc: todayAtUtc(startHour), endsAtUtc: todayAtUtc(startHour, durationMinutes),
    durationMinutes, state, source, note,
    createdAtUtc: todayAtUtc(8), updatedAtUtc: todayAtUtc(8), cancelledAtUtc: null, cancelReason: ''
  };
}

// Набор на день: две онлайн-заявки без места (уходят в лейн «новых заявок») + размещённые брони
// разных статусов на дорожках мест, чтобы превью показывало все тона таймлайна и drawer.
function reservations() {
  const pc01 = { id: 'a1', name: 'PC-01', zone: 'Зал A' };
  const pc02 = { id: 'a2', name: 'PC-02', zone: 'Зал A' };
  const pc06 = { id: 'a6', name: 'PC-06', zone: 'Зал A' };
  const vip03 = { id: 'b3', name: 'VIP-03', zone: 'VIP' };
  return [
    booking('r1', 10, 90, 'seated', 'operator', 'Дилноза Х.', '+992900000001', pc06, 'посажена на место'),
    booking('r2', 12, 120, 'confirmed', 'online', 'Азиз П.', '+992900000002', pc02, 'онлайн-заявка, подтверждена'),
    booking('r3', 14, 60, 'pending', 'online', 'Камрон Р.', '+992900000003', null, 'онлайн-заявка'),
    booking('r4', 16, 90, 'pending', 'online', 'Сабина М.', '+992900000004', null, 'онлайн-заявка, ждёт места'),
    booking('r5', 18, 60, 'confirmed', 'operator', 'Фаррух Н.', '+992900000005', vip03, 'бронь оператора'),
    booking('r6', 20, 60, 'pending', 'operator', 'Шерзод Б.', '+992900000006', pc01, 'предварительная бронь')
  ];
}

// Деталь устройства для «Статус ПК»: machineName опускаем — описатель подставит имя места,
// версии/состояние реальные, чтобы кнопка «Статус» показывала осмысленный отчёт в превью.
function deviceDetail() {
  return {
    organizationId: ORG, branchId: BRANCH,
    deviceId: 'preview-device', agentVersion: '0.4', shellVersion: '0.4',
    isOnline: true, isLocked: true,
    enrolledAtUtc: '2026-05-21T08:30:00Z', lastHeartbeatAtUtc: '2026-05-21T10:00:00Z',
    activeCredentialCount: 1, installedAppCount: 2, recentCommands: []
  };
}

function diagnostics() {
  return {
    organizationId: ORG, branchId: BRANCH, generatedAtUtc: '2026-05-21T10:00:00Z',
    deviceSummary: { totalDevices: 10, onlineDevices: 8, lockedDevices: 5, staleDevices: 0, staleThresholdSeconds: 120, newestHeartbeatAtUtc: '2026-05-21T10:00:00Z' },
    commandSummary: { pendingCommands: 0, failedCommands: 0, recentFailures: [] },
    updateSummary: { activeRollouts: 0, installingDevices: 0, failedDevices: 0, rollbackDevices: 0, recentFailures: [] },
    staleDevices: []
  };
}

// Route a platform request to a fixture. Returns null when nothing matches, so the caller can apply
// a safe default.
function route(pathname: string, method: string): unknown | undefined {
  if (pathname.endsWith('/floor-map')) return floorMap();
  if (pathname.endsWith('/dashboard/summary')) return dashboardSummary();
  if (pathname.endsWith('/shifts/current')) return currentShift();
  if (pathname.endsWith('/pos/catalog')) return [posProduct()];
  if (pathname.endsWith('/reservations') && method === 'GET') return { reservations: reservations(), limit: 40 };
  if (pathname.endsWith('/inventory/stock-movements') && method === 'GET') return [];
  if (pathname.endsWith('/commands') && method === 'GET') return [];
  if (pathname.endsWith('/diagnostics') && method === 'GET') return diagnostics();
  if (pathname.includes('/devices/') && method === 'GET') return deviceDetail();
  return undefined;
}

// Клиенты клуба для поиска в брони/POS: фильтр по имени или цифрам телефона.
function players() {
  return [
    { playerAccountId: 'pl-1', displayName: 'Фариза Назарова', phoneNumber: '+992 93 100 20 30', walletBalanceMinorUnits: 45000, debtBalanceMinorUnits: 0, activePackageCount: 1, isActive: true },
    { playerAccountId: 'pl-2', displayName: 'Азиз Пиров', phoneNumber: '+992 90 555 22 11', walletBalanceMinorUnits: 12000, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: true },
    { playerAccountId: 'pl-3', displayName: 'Мадина Саидова', phoneNumber: '+992 98 700 11 22', walletBalanceMinorUnits: 0, debtBalanceMinorUnits: 3500, activePackageCount: 0, isActive: true },
    { playerAccountId: 'pl-4', displayName: 'Камрон Рахимов', phoneNumber: '+992 92 333 44 55', walletBalanceMinorUnits: 8000, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: true },
    { playerAccountId: 'pl-5', displayName: 'Дилноза Холова', phoneNumber: '+992 91 222 33 44', walletBalanceMinorUnits: 26000, debtBalanceMinorUnits: 0, activePackageCount: 2, isActive: true }
  ];
}

function filterPlayers(query: string | null): ReturnType<typeof players> {
  const q = (query ?? '').trim().toLowerCase();
  if (!q) return players();
  const digits = q.replace(/\D/g, '');
  return players().filter((p) =>
    p.displayName.toLowerCase().includes(q)
    || (digits.length > 0 && p.phoneNumber.replace(/\D/g, '').includes(digits)));
}

export async function devMockFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const url = new URL(String(input));
  const method = init?.method ?? 'GET';
  if (url.pathname.endsWith('/players') && method === 'GET') {
    return json(filterPlayers(url.searchParams.get('query')));
  }
  const matched = route(url.pathname, method);
  if (matched !== undefined) {
    return json(matched);
  }
  // Writes acknowledge with no content; unmatched reads return an empty list so list-driven screens
  // render their themed empty state instead of throwing.
  if (method !== 'GET') {
    return noContent();
  }
  return json([]);
}
