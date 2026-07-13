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

// Browser preview is intentionally stateful for the small set of actions that
// reviewers exercise locally. It is reset by a full page reload because the
// module is re-evaluated; production data never reaches this store.
let previewFloorMap: ReturnType<typeof floorMap> | null = null;
let previewSessionSequence = 1;

function currentPreviewFloorMap(): ReturnType<typeof floorMap> {
  if (previewFloorMap === null) {
    previewFloorMap = structuredClone(floorMap());
  }
  return previewFloorMap;
}

function previewLayoutZones() {
  const zones = new Map<string, { zoneId: string; name: string; sortOrder: number; seats: Array<Record<string, unknown>> }>();
  for (const seat of currentPreviewFloorMap().seats) {
    const zone = zones.get(seat.zoneId) ?? {
      zoneId: seat.zoneId,
      name: seat.zoneName,
      sortOrder: zones.size + 1,
      seats: []
    };
    zone.seats.push({ seatId: seat.seatId, name: seat.seatName, sortOrder: seat.sortOrder });
    zones.set(seat.zoneId, zone);
  }
  return [...zones.values()];
}

function previewStaff() {
  return [{
    staffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
    displayName: 'Оператор смены',
    login: 'operator-preview',
    isActive: true,
    roles: ['branch_manager']
  }];
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

function currentShiftRevenue() {
  const m = (minorUnits: number) => money(minorUnits);
  return {
    shiftId: 'sh1', organizationId: ORG, branchId: BRANCH,
    openedByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134', closedByStaffUserId: null, state: 'open',
    earned: { time: m(82000), goods: m(41000), total: m(123000) },
    inflow: { cash: m(90000), nonCash: m(33000), walletTopUps: m(15000), directTotal: m(123000) },
    // expected (касса в ящике) = netCashTotal движений (58000) + наличные продажи (90000) = 148000.
    cash: { starting: m(100000), expected: m(148000), counted: null, difference: null },
    openedAtUtc: '2026-05-21T08:00:00Z', closedAtUtc: null
  };
}

// История закрытых смен для вкладки «Смена». Три прошлых дня с разными расхождениями
// (недостача / в ноль / излишек), чтобы превью показало все тона строки истории.
function shiftHistory() {
  const closed = (shiftId: string, day: string, earnedTotal: number, differenceMinor: number) => {
    const cashSales = Math.round(earnedTotal * 0.7);
    const expected = 100000 + cashSales;
    return {
      shiftId, organizationId: ORG, branchId: BRANCH,
      openedByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134', closedByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
      state: 'closed',
      earned: { time: money(Math.round(earnedTotal * 0.66)), goods: money(earnedTotal - Math.round(earnedTotal * 0.66)), total: money(earnedTotal) },
      inflow: { cash: money(cashSales), nonCash: money(Math.round(earnedTotal * 0.2)), walletTopUps: money(Math.round(earnedTotal * 0.1)), directTotal: money(earnedTotal) },
      cash: { starting: money(100000), expected: money(expected), counted: money(expected + differenceMinor), difference: money(differenceMinor) },
      openedAtUtc: `${day}T08:00:00Z`, closedAtUtc: `${day}T22:00:00Z`
    };
  };
  return {
    shifts: [
      closed('sh-prev-1', '2026-05-20', 234000, -5000),
      closed('sh-prev-2', '2026-05-19', 198000, 0),
      closed('sh-prev-3', '2026-05-18', 275000, 12000)
    ],
    limit: 20
  };
}

// Приходно-расходные операции кассового ящика (НЕ продажи): открытие · внесение · изъятие · возврат.
// Лента в «Смене» (последние 6) и «Журнале кассы» (полный список + поиск + сводка).
function cashOperationReport() {
  const row = (operationId: string, operationType: string, impactMinor: number, reason: string, minutesBack: number) =>
    ({ operationId, operationType, cashImpact: money(impactMinor), reason, createdAtUtc: minutesAgoUtc(minutesBack) });
  return {
    cashInTotal: money(150000),  // открытие 1000 + внесение 500
    cashOutTotal: money(92000),  // изъятие 800 + возврат 120
    netCashTotal: money(58000),  // 1500 − 920
    rows: [
      row('co-04', 'cash_out', -80000, 'Инкассация в сейф', 30),
      row('co-03', 'cash_in', 50000, 'Доложен разменный фонд', 120),
      row('co-02', 'refund', -12000, 'Возврат за отменённый заказ', 180),
      row('co-01', 'opening', 100000, 'Открытие смены · разменный фонд', 240)
    ]
  };
}

// Отчёт по продажам POS — «Последние чеки» в кассе + строка-метрика «Продажи N · сумма».
// Суммы оплаченных чеков складываются в grossSalesTotal (41000 = goods-выручка смены).
function salesReport() {
  const row = (posSaleId: string, state: string, totalMinor: number, lineCount: number, itemQuantity: number, minutesBack: number) =>
    ({ posSaleId, state, total: money(totalMinor), lineCount, itemQuantity, createdAtUtc: minutesAgoUtc(minutesBack) });
  return {
    grossSalesTotal: money(41000),
    refundsTotal: money(3000),
    rows: [
      row('ps-06', 'paid', 1200, 1, 1, 10),
      row('ps-05', 'paid', 5600, 2, 3, 40),
      row('ps-04', 'paid', 2500, 1, 1, 60),
      row('ps-03', 'paid', 12000, 3, 4, 90),
      row('ps-02', 'paid', 19700, 3, 5, 120),
      row('ps-01', 'refunded', 3000, 1, 1, 150)
    ]
  };
}

// Очередь заказов из Player Shell (Заказы): игрок оформляет с места, касса лишь меняет статус.
// placed → ждёт принятия; accepted → ждёт выдачи. seatId показывается как есть (читаемое имя места).
function shopOrders() {
  const line = (productId: string, name: string, unitMinor: number, quantity: number) =>
    ({ productId, name, unitPrice: money(unitMinor), quantity, lineTotal: money(unitMinor * quantity) });
  return [
    { id: 'so-1', branchId: BRANCH, seatId: 'PC-01', playerAccountId: 'pl-2', playerDisplayName: 'Амир Каримов', status: 'placed', total: money(8600), lines: [line('prod-hotdog', 'Хот-дог', 2800, 1), line('prod-cola', 'Cola 0.5', 1200, 1), line('prod-chips', 'Чипсы Lays', 1400, 2), line('prod-energy', 'Энергетик Red Bull', 1800, 1)], placedAtUtc: minutesAgoUtc(5), acceptedAtUtc: null, deliveredAtUtc: null, cancelledAtUtc: null, version: 1 },
    { id: 'so-2', branchId: BRANCH, seatId: 'PC-09', playerAccountId: 'pl-4', playerDisplayName: 'Юсуф Ахмедов', status: 'accepted', total: money(1200), lines: [line('prod-cola', 'Cola 0.5', 1200, 1)], placedAtUtc: minutesAgoUtc(15), acceptedAtUtc: minutesAgoUtc(10), deliveredAtUtc: null, cancelledAtUtc: null, version: 2 },
    { id: 'so-3', branchId: BRANCH, seatId: 'VIP-01', playerAccountId: 'pl-3', playerDisplayName: 'Мадина Саидова', status: 'placed', total: money(3600), lines: [line('prod-energy', 'Энергетик Red Bull', 1800, 2)], placedAtUtc: minutesAgoUtc(2), acceptedAtUtc: null, deliveredAtUtc: null, cancelledAtUtc: null, version: 1 }
  ];
}

// Каталог POS: напитки / снеки / услуги, с парой позиций на нуле и низком остатке —
// чтобы превью показывало чипы категорий, метрику «Склад N низко» и реальные продажи.
function posCatalog() {
  const p = (productId: string, categoryName: string, name: string, sku: string, priceMinor: number, stockOnHand: number, trackStock = true) =>
    ({ productId, organizationId: ORG, branchId: BRANCH, categoryId: categoryName, categoryName, name, sku, price: money(priceMinor), trackStock, allowNegativeStock: false, isActive: true, stockOnHand, createdAtUtc: '2026-05-21T08:00:00Z' });
  return [
    p('prod-cola', 'Напитки', 'Cola 0.5', 'COLA-05', 1200, 12),
    p('prod-water', 'Напитки', 'Вода 0.5', 'WATER-05', 600, 30),
    p('prod-energy', 'Напитки', 'Энергетик Red Bull', 'ENERGY-RB', 1800, 8),
    p('prod-juice', 'Напитки', 'Сок апельсиновый', 'JUICE-OR', 1500, 0),
    p('prod-hotdog', 'Снеки', 'Хот-дог', 'HOTDOG', 2800, 6),
    p('prod-chips', 'Снеки', 'Чипсы Lays', 'CHIPS-LAYS', 1400, 2),
    p('prod-snickers', 'Снеки', 'Шоколад Snickers', 'SNICKERS', 1100, 15),
    p('prod-guesthour', 'Услуги', 'Гостевой час', 'GUEST-HOUR', 2500, 0, false),
    p('prod-headset', 'Услуги', 'Аренда наушников', 'HEADSET', 500, 0, false)
  ];
}

function stockMovementsFixture() {
  return [
    { stockMovementId: 'mv-1', productId: 'prod-cola', movementType: 'sale', quantityDelta: -1, unitCost: { currencyCode: 'TJS', minorUnits: 400 }, reason: 'чек #1042', createdByStaffUserId: 'staff-1', createdByDisplayName: 'Олег С.', createdAtUtc: todayAtUtc(13, 42) },
    { stockMovementId: 'mv-2', productId: 'prod-chips', movementType: 'adjustment', quantityDelta: -2, unitCost: { currencyCode: 'TJS', minorUnits: 600 }, reason: 'брак упаковки', createdByStaffUserId: 'staff-1', createdByDisplayName: 'Олег С.', createdAtUtc: todayAtUtc(13, 30) },
    { stockMovementId: 'mv-3', productId: 'prod-energy', movementType: 'purchase', quantityDelta: 24, unitCost: { currencyCode: 'TJS', minorUnits: 900 }, reason: 'Приёмка · Напитки Душанбе', createdByStaffUserId: 'staff-1', createdByDisplayName: 'Олег С.', createdAtUtc: todayAtUtc(10, 5) },
  ];
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

// «N дней назад» от текущего момента — для createdAtUtc/lastActivityAtUtc клиентов в превью.
function daysAgoUtc(days: number): string {
  return minutesAgoUtc(days * 24 * 60);
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
  note: string,
  groupId: string | null = null,
  playerAccountId: string | null = null
) {
  return {
    reservationId: id, organizationId: ORG, branchId: BRANCH, playerAccountId,
    reservationGroupId: groupId,
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
    booking('r6', 20, 60, 'pending', 'operator', 'Шерзод Б.', '+992900000006', pc01, 'предварительная бронь'),
    // Бронь конкретного клиента (pl-1) — чтобы профиль показывал полосу «ближайшая бронь».
    booking('rp1', 19, 60, 'confirmed', 'operator', 'Фариза Назарова', '+992 93 100 20 30', vip03, 'бронь клиента', null, 'pl-1'),
    // Массовая бронь: две брони на разных ПК с общим reservationGroupId (для превью связи группы).
    booking('g1', 15, 60, 'confirmed', 'operator', 'Турнир «Алиф»', '+992900000007', pc02, 'массовая бронь · 2 ПК', 'grp-demo'),
    booking('g2', 15, 60, 'confirmed', 'operator', 'Турнир «Алиф»', '+992900000007', pc06, 'массовая бронь · 2 ПК', 'grp-demo')
  ];
}

// Сессии для таймлайна: две завершённые сегодня (история, закрытые полосы) + активные с floor-map
// (открытый таб без конца и две ограниченные дедлайном). minutesAgoUtc(-N) = «сейчас + N мин».
function sessionsTimeline() {
  return [
    { sessionId: 'h1', seatId: 'a1', seatName: 'PC-01', zoneId: 'z-a', zoneName: 'Зал A', state: 'ended', playerAccountId: null, playerDisplayName: 'Дилноза Х.', tariffName: 'Стандарт', startedAtUtc: todayAtUtc(4, 0), endsAtUtc: null, endedAtUtc: todayAtUtc(5, 0) },
    { sessionId: 'h2', seatId: 'c1', seatName: 'PC-07', zoneId: 'z-b', zoneName: 'Зал B', state: 'ended', playerAccountId: null, playerDisplayName: 'Рустам К.', tariffName: 'Почасовой', startedAtUtc: todayAtUtc(6, 30), endsAtUtc: null, endedAtUtc: todayAtUtc(8, 0) },
    { sessionId: 's3', seatId: 'a3', seatName: 'PC-03', zoneId: 'z-a', zoneName: 'Зал A', state: 'active', playerAccountId: null, playerDisplayName: 'Юсуф А.', tariffName: 'Почасовой', startedAtUtc: minutesAgoUtc(110), endsAtUtc: null, endedAtUtc: null },
    // pl-1 (Фариза) играет сейчас на PC-01 с дедлайном → полоса «играет · до HH:MM» на её профиле.
    { sessionId: 's1', seatId: 'a1', seatName: 'PC-01', zoneId: 'z-a', zoneName: 'Зал A', state: 'active', playerAccountId: 'pl-1', playerDisplayName: 'Фариза Назарова', tariffName: 'Стандарт', startedAtUtc: minutesAgoUtc(75), endsAtUtc: minutesAgoUtc(-43), endedAtUtc: null },
    // pl-3 (Мадина) — открытый счёт на VIP-01.
    { sessionId: 's5', seatId: 'b1', seatName: 'VIP-01', zoneId: 'z-vip', zoneName: 'VIP', state: 'active', playerAccountId: 'pl-3', playerDisplayName: 'Мадина Саидова', tariffName: 'VIP час', startedAtUtc: minutesAgoUtc(50), endsAtUtc: minutesAgoUtc(-90), endedAtUtc: null }
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

// Реалистичный счёт для окна завершения сессии: наигранное время + снеки + депозит игрока,
// чтобы в превью была видна вся касса (вкладки/купюры/сдача), а не пустое «оплата не требуется».
function checkoutQuote() {
  return {
    sessionId: 's-preview',
    timeCharge: money(5000),
    posTotal: money(450),
    grandTotal: money(5450),
    billableSeconds: 3720,
    playerAccountId: null,
    walletBalance: money(3000)
  };
}

// Эхо успешной оплаты — чтобы подтверждение кассы в превью отрабатывало без ошибки.
function checkoutResult(init?: RequestInit) {
  let req: Record<string, unknown> = {};
  try { req = JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>; } catch { req = {}; }
  return {
    idempotencyKey: (req.idempotencyKey as string) ?? 'session-checkout-preview',
    sessionId: 's-preview',
    timeCharge: money(5000),
    posTotal: money(450),
    grandTotal: money(5450),
    payments: Array.isArray(req.payments) ? req.payments : [],
    receipt: {},
    session: { sessionId: 's-preview', state: 'Ending' },
    deviceCommands: []
  };
}

// Тарифы для окна запуска сессии: ставка за минуту → видна цена-превью и покрытие баланса.
function tariffOptions() {
  return [
    { tariffVersionId: 'tv-hourly', tariffRuleVersionId: 'rule-hourly', name: 'Почасовой', pricePerMinuteMinorUnits: 80, currencyCode: 'TJS' },
    { tariffVersionId: 'tv-vip', tariffRuleVersionId: 'rule-vip', name: 'VIP час', pricePerMinuteMinorUnits: 150, currencyCode: 'TJS' },
    { tariffVersionId: 'tv-night', tariffRuleVersionId: 'rule-night', name: 'Ночной', pricePerMinuteMinorUnits: 50, currencyCode: 'TJS' }
  ];
}

// Route a platform request to a fixture. Returns null when nothing matches, so the caller can apply
// a safe default.
function route(pathname: string, method: string): unknown | undefined {
  if (pathname.endsWith('/checkout/quote') && method === 'GET') return checkoutQuote();
  if (pathname.endsWith('/tariffs/options')) return tariffOptions();
  if (pathname.endsWith('/floor-map')) return currentPreviewFloorMap();
  if (pathname.endsWith('/layout/zones')) return previewLayoutZones();
  if (pathname.endsWith('/staff')) return previewStaff();
  if (pathname.endsWith('/dashboard/summary')) return dashboardSummary();
  if (pathname.endsWith('/shifts/revenue/current')) return currentShiftRevenue();
  if (pathname.endsWith('/shifts/revenue')) return shiftHistory();
  if (pathname.endsWith('/shifts/current')) return currentShift();
  if (pathname.endsWith('/reports/cash-operations') && method === 'GET') return cashOperationReport();
  if (pathname.endsWith('/reports/sales') && method === 'GET') return salesReport();
  if (pathname.endsWith('/shop/orders') && method === 'GET') return shopOrders();
  if (pathname.endsWith('/pos/catalog')) return posCatalog();
  if (pathname.endsWith('/reservations') && method === 'GET') return { reservations: reservations(), limit: 40 };
  if (pathname.endsWith('/sessions') && method === 'GET') return { sessions: sessionsTimeline() };
  if (pathname.endsWith('/inventory/stock-movements') && method === 'GET') return stockMovementsFixture();
  if (pathname.endsWith('/inventory/stock-movements') && method === 'POST') return { stockMovementId: 'mock-movement' };
  if (pathname.endsWith('/commands') && method === 'GET') return [];
  if (pathname.endsWith('/diagnostics') && method === 'GET') return diagnostics();
  if (pathname.includes('/devices/') && method === 'GET') return deviceDetail();
  return undefined;
}

// Клиенты клуба для поиска: мутируемые (write-действия S3 меняют имя/активность), один неактивный.
type MockPlayer = {
  playerAccountId: string; displayName: string; phoneNumber: string; walletBalanceMinorUnits: number;
  debtBalanceMinorUnits: number; activePackageCount: number; isActive: boolean;
  createdAtUtc: string; lastActivityAtUtc: string | null;
  activePackageName: string | null; activePackageRemainingMinutes: number;
};
let mutablePlayers: MockPlayer[] | null = null;
function players(): MockPlayer[] {
  if (mutablePlayers === null) {
    mutablePlayers = [
      // Давний клиент, играет прямо сейчас — тег «Новый» не горит, визит «сейчас», есть банк времени.
      { playerAccountId: 'pl-1', displayName: 'Фариза Назарова', phoneNumber: '+992 93 100 20 30', walletBalanceMinorUnits: 45000, debtBalanceMinorUnits: 0, activePackageCount: 1, isActive: true, createdAtUtc: daysAgoUtc(400), lastActivityAtUtc: minutesAgoUtc(15), activePackageName: 'Ночной 5ч', activePackageRemainingMinutes: 150 },
      // Зарегистрирован 3 дня назад (< 7 — тег «Новый») и заходил вчера.
      { playerAccountId: 'pl-2', displayName: 'Азиз Пиров', phoneNumber: '+992 90 555 22 11', walletBalanceMinorUnits: 12000, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: true, createdAtUtc: daysAgoUtc(3), lastActivityAtUtc: daysAgoUtc(1), activePackageName: null, activePackageRemainingMinutes: 0 },
      { playerAccountId: 'pl-3', displayName: 'Мадина Саидова', phoneNumber: '+992 98 700 11 22', walletBalanceMinorUnits: 0, debtBalanceMinorUnits: 3500, activePackageCount: 0, isActive: true, createdAtUtc: daysAgoUtc(200), lastActivityAtUtc: daysAgoUtc(3), activePackageName: null, activePackageRemainingMinutes: 0 },
      // Визит 10 дней назад — колонка показывает «недели», а не «дни».
      { playerAccountId: 'pl-4', displayName: 'Камрон Рахимов', phoneNumber: '+992 92 333 44 55', walletBalanceMinorUnits: 8000, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: true, createdAtUtc: daysAgoUtc(150), lastActivityAtUtc: daysAgoUtc(10), activePackageName: null, activePackageRemainingMinutes: 0 },
      { playerAccountId: 'pl-5', displayName: 'Дилноза Холова', phoneNumber: '+992 91 222 33 44', walletBalanceMinorUnits: 26000, debtBalanceMinorUnits: 0, activePackageCount: 2, isActive: true, createdAtUtc: daysAgoUtc(60), lastActivityAtUtc: daysAgoUtc(2), activePackageName: 'Дневной абонемент', activePackageRemainingMinutes: 420 },
      // Неактивный, визитов не было — проверяет пустое состояние «—».
      { playerAccountId: 'pl-6', displayName: 'Бахром Сафаров', phoneNumber: '+992 93 444 55 66', walletBalanceMinorUnits: 0, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: false, createdAtUtc: daysAgoUtc(500), lastActivityAtUtc: null, activePackageName: null, activePackageRemainingMinutes: 0 }
    ];
  }
  return mutablePlayers;
}

function filterPlayers(query: string | null, includeInactive: boolean): MockPlayer[] {
  const q = (query ?? '').trim().toLowerCase();
  const digits = q.replace(/\D/g, '');
  return players().filter((p) => {
    if (!includeInactive && !p.isActive) return false;
    if (!q) return true;
    return p.displayName.toLowerCase().includes(q)
      || (digits.length > 0 && p.phoneNumber.replace(/\D/g, '').includes(digits));
  });
}

// Длинный детерминированный журнал операций клиента для превью пагинации/фильтра. Фикс-даты
// (без Date.now) — keyset стабилен между рендерами. 48 записей разных типов.
const LEDGER_BASE_DAY = '2026-05-13';
function ledgerLog(): Array<Record<string, unknown>> {
  const staff = '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134';
  // Курируемый цикл типов, чтобы фильтр-чипы давали непустые наборы.
  const cycle: Array<{ type: string; account: string; amount: number; description: string; reason: string }> = [
    { type: 'top_up', account: 'wallet', amount: 50000, description: 'Пополнение кошелька', reason: 'Касса' },
    { type: 'gameplay_charge', account: 'wallet', amount: -12000, description: 'Списание за игру', reason: 'Сессия PC-03' },
    { type: 'package_purchase', account: 'wallet', amount: -25000, description: 'Покупка пакета «Ночной 5ч»', reason: 'Пакет' },
    { type: 'debt_payment', account: 'debt', amount: 3500, description: 'Погашение долга', reason: 'Касса' },
    { type: 'refund', account: 'wallet', amount: -5000, description: 'Возврат операции', reason: 'Ошибочное списание' },
    { type: 'manual_correction', account: 'wallet', amount: 2000, description: 'Ручная корректировка', reason: 'Сверка кассы' }
  ];
  const log: Array<Record<string, unknown>> = [];
  for (let i = 0; i < 48; i++) {
    const spec = cycle[i % cycle.length];
    // Время убывает с ростом i: самые свежие записи — с меньшим i (час 23 → вниз).
    const minutesBack = i * 17;
    const totalMinutes = 23 * 60 - minutesBack;
    const hh = String(Math.max(0, Math.floor(totalMinutes / 60))).padStart(2, '0');
    const mm = String(((totalMinutes % 60) + 60) % 60).padStart(2, '0');
    const isRefund = spec.type === 'refund';
    log.push({
      ledgerEntryId: `le-${String(i + 1).padStart(3, '0')}`,
      organizationId: ORG, branchId: BRANCH, playerAccountId: 'pl-1', sessionId: null, playerPackageId: null,
      entryType: spec.type, accountType: spec.account, amount: money(spec.amount),
      quantitySeconds: 0, description: spec.description, reason: spec.reason,
      reversesLedgerEntryId: isRefund ? 'le-001' : null,
      createdByStaffUserId: staff,
      createdAtUtc: `${LEDGER_BASE_DAY}T${hh}:${mm}:00Z`
    });
  }
  return log; // уже newest-first (i=0 — самая свежая)
}

// Мутируемое хранилище журнала для превью: write-действия (корректировка/возврат) добавляют сюда
// записи, чтобы История/баланс в превью обновлялись. Лениво инициализируется из ledgerLog().
let mutableLedger: Array<Record<string, unknown>> | null = null;
function ledger(): Array<Record<string, unknown>> {
  if (mutableLedger === null) mutableLedger = ledgerLog();
  return mutableLedger;
}
let nextLedgerSeq = 1000;
let nextPosSaleSeq = 1;
function prependLedger(entry: Record<string, unknown>): void {
  ledger().unshift(entry);
}

// Keyset-страница для /ledger: фильтр по entryType/accountType, курсор по индексу записи в журнале.
// Курсор = base64(`${index}`) — простой и детерминированный (на бэке курсор иной, но клиенту он
// непрозрачен, важна лишь корректность «следующей страницы»).
function ledgerPage(searchParams: URLSearchParams): { items: Array<Record<string, unknown>>; nextCursor: string | null } {
  const entryType = searchParams.get('entryType');
  const accountType = searchParams.get('accountType');
  const before = searchParams.get('before');
  const limit = Math.min(Math.max(Number.parseInt(searchParams.get('limit') ?? '50', 10) || 50, 1), 100);

  let all = ledger();
  if (entryType) all = all.filter((e) => e.entryType === entryType);
  if (accountType) all = all.filter((e) => e.accountType === accountType);

  let start = 0;
  if (before) {
    try {
      const decoded = Number.parseInt(atob(before), 10);
      if (Number.isFinite(decoded)) start = decoded;
    } catch { start = 0; }
  }

  const items = all.slice(start, start + limit);
  const nextIndex = start + limit;
  const nextCursor = nextIndex < all.length ? btoa(String(nextIndex)) : null;
  return { items, nextCursor };
}

// Сумма wallet-проводок в исходном журнале — вычитаем, чтобы стартовый баланс превью был 45000.
const ledgerLogWalletBaseline = ledgerLog()
  .filter((e) => e.accountType === 'wallet')
  .reduce((acc, e) => acc + ((e.amount as { minorUnits: number }).minorUnits ?? 0), 0);

function walletSummary() {
  const log = ledger();
  const sumByAccount = (account: string) =>
    log.filter((e) => e.accountType === account)
      .reduce((acc, e) => acc + ((e.amount as { minorUnits: number }).minorUnits ?? 0), 0);
  const wallet = 45000 + sumByAccount('wallet') - ledgerLogWalletBaseline;
  const debt = Math.max(0, sumByAccount('debt'));
  return { playerAccountId: 'pl-1', walletBalance: money(wallet), debtBalance: money(debt), recentEntries: log.slice(0, 5) };
}

function playerPackages() {
  return [
    { playerPackageId: 'pp-1', name: 'Ночной 5ч', purchasedPrice: money(25000), includedSeconds: 18000, bonusSeconds: 1800, remainingIncludedSeconds: 9000, remainingBonusSeconds: 1800, purchasedAtUtc: minutesAgoUtc(1440), expiresAtUtc: FAR_FUTURE }
  ];
}

// Массовая бронь в превью: эхо успешного результата по seatId из тела (без конфликтов).
function groupReservationResult(init?: RequestInit): unknown {
  let req: Record<string, unknown> = {};
  try { req = JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>; } catch { req = {}; }
  const seatIds = Array.isArray(req.seatIds) ? (req.seatIds as string[]) : [];
  const startsAtUtc = typeof req.startsAtUtc === 'string' ? req.startsAtUtc : todayAtUtc(12);
  const groupId = 'grp-preview';
  const reservations = seatIds.map((seatId, i) => ({
    reservationId: `grp-${i}`, organizationId: ORG, branchId: BRANCH, reservationGroupId: groupId,
    playerAccountId: (req.playerAccountId as string | null) ?? null, seatId, seatName: '', zoneName: '',
    customerName: (req.customerName as string) ?? 'Группа', phoneNumber: (req.phoneNumber as string | null) ?? null,
    startsAtUtc, endsAtUtc: startsAtUtc, durationMinutes: (req.durationMinutes as number) ?? 60,
    state: 'confirmed', source: (req.source as string) ?? 'operator', note: (req.note as string) ?? '',
    createdAtUtc: startsAtUtc, updatedAtUtc: startsAtUtc, cancelledAtUtc: null, cancelReason: ''
  }));
  return { reservationGroupId: groupId, reservations, conflicts: [] };
}

export async function devMockFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const url = new URL(String(input));
  const method = init?.method ?? 'GET';
  if (url.pathname.endsWith('/players') && method === 'GET') {
    return json(filterPlayers(url.searchParams.get('query'), url.searchParams.get('includeInactive') === 'true'));
  }
  if (url.pathname.endsWith('/reservations/group') && method === 'POST') {
    return json(groupReservationResult(init));
  }
  if (url.pathname.endsWith('/sessions/start') && method === 'POST') {
    let request: Record<string, unknown> = {};
    try { request = JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>; } catch { request = {}; }
    const seatId = typeof request.seatId === 'string' ? request.seatId : '';
    const seat = currentPreviewFloorMap().seats.find((item) => item.seatId === seatId);
    if (seat === undefined || seat.state !== 'Free') {
      return new Response(JSON.stringify({ message: 'Preview seat is not ready.' }), { status: 409, headers: { 'Content-Type': 'application/json' } });
    }

    const isOpenTab = request.durationMode === 'open';
    const sessionId = `preview-session-${previewSessionSequence++}`;
    seat.state = 'Active';
    seat.activeSessionId = sessionId;
    seat.sessionStartedAtUtc = new Date().toISOString();
    seat.playerDisplayName = undefined;
    seat.tariffName = 'Почасовой';
    seat.remainingSeconds = isOpenTab ? null : Number(request.durationMinutes ?? 60) * 60;
    seat.accruedCostMinorUnits = isOpenTab ? 0 : undefined;
    seat.isDeviceLocked = false;

    return json({
      idempotencyKey: typeof request.idempotencyKey === 'string' ? request.idempotencyKey : 'preview-session-start',
      session: { sessionId, state: 'Active' },
      deviceCommands: []
    });
  }
  if (url.pathname.endsWith('/checkout') && method === 'POST') {
    return json(checkoutResult(init));
  }
  if (url.pathname.endsWith('/wallet-summary') && method === 'GET') {
    return json(walletSummary());
  }
  if (url.pathname.includes('/players/') && url.pathname.endsWith('/packages') && method === 'GET') {
    return json(playerPackages());
  }
  if (url.pathname.includes('/players/') && url.pathname.endsWith('/ledger') && method === 'GET') {
    return json(ledgerPage(url.searchParams));
  }
  if (url.pathname.endsWith('/packages/purchases') && method === 'POST') {
    return json(playerPackages()[0]);
  }
  if ((url.pathname.endsWith('/wallet/top-ups') || url.pathname.endsWith('/debts/payments')) && method === 'POST') {
    return json(walletSummary());
  }
  // Брони с фильтром по клиенту (профиль «Клиенты» спрашивает ближайшую бронь конкретного игрока).
  // Без фильтра (экран «Брони») возвращаем весь набор. Имитирует серверный playerAccountId-фильтр.
  if (url.pathname.endsWith('/reservations') && method === 'GET') {
    const playerAccountId = url.searchParams.get('playerAccountId');
    const all = reservations();
    const filtered = playerAccountId ? all.filter((r) => r.playerAccountId === playerAccountId) : all;
    return json({ reservations: filtered, limit: 40 });
  }
  const matched = route(url.pathname, method);
  if (matched !== undefined) {
    return json(matched);
  }
  if (url.pathname.endsWith('/ledger/manual-corrections') && method === 'POST') {
    let req: Record<string, unknown> = {};
    try { req = JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>; } catch { req = {}; }
    const amount = req.amount as { currencyCode: string; minorUnits: number } | undefined;
    prependLedger({
      ledgerEntryId: `le-c${nextLedgerSeq++}`, organizationId: ORG, branchId: BRANCH, playerAccountId: 'pl-1',
      sessionId: null, playerPackageId: null, entryType: 'manual_correction',
      accountType: (req.accountType as string) ?? 'wallet', amount: amount ?? money(0),
      quantitySeconds: 0, description: 'Ручная корректировка', reason: (req.reason as string) ?? '',
      reversesLedgerEntryId: null, createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
      createdAtUtc: minutesAgoUtc(0)
    });
    return json(walletSummary());
  }
  if (url.pathname.endsWith('/refunds') && url.pathname.includes('/ledger/') && method === 'POST') {
    let req: Record<string, unknown> = {};
    try { req = JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>; } catch { req = {}; }
    const amount = req.amount as { currencyCode: string; minorUnits: number } | undefined;
    const reversedId = (req.ledgerEntryId as string) ?? null;
    const entry = {
      ledgerEntryId: `le-r${nextLedgerSeq++}`, organizationId: ORG, branchId: BRANCH, playerAccountId: 'pl-1',
      sessionId: null, playerPackageId: null, entryType: 'refund', accountType: 'wallet',
      amount: money(-Math.abs(amount?.minorUnits ?? 0)), quantitySeconds: 0,
      description: 'Возврат операции', reason: (req.reason as string) ?? '',
      reversesLedgerEntryId: reversedId, createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
      createdAtUtc: minutesAgoUtc(0)
    };
    prependLedger(entry);
    return json(entry);
  }
  if (url.pathname.endsWith('/pos/sales') && method === 'POST') {
    return json({ posSaleId: `ps-${nextPosSaleSeq++}`, state: 'created' });
  }
  if (url.pathname.includes('/pos/sales/') && url.pathname.endsWith('/payments/manual') && method === 'POST') {
    return json({ paymentId: `pp-${nextPosSaleSeq}`, posSaleId: url.pathname.split('/').slice(-3)[0], state: 'paid' });
  }
  if (url.pathname.endsWith('/pin') && method === 'POST') {
    return noContent();
  }
  if (url.pathname.endsWith('/active-state') && method === 'POST') {
    let req: Record<string, unknown> = {};
    try { req = JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>; } catch { req = {}; }
    const id = url.pathname.split('/').slice(-2)[0];
    const player = players().find((p) => p.playerAccountId === id);
    if (player) player.isActive = Boolean(req.isActive);
    return json(player ?? {});
  }
  if (/\/players\/[^/]+$/.test(url.pathname) && method === 'PATCH') {
    let req: Record<string, unknown> = {};
    try { req = JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>; } catch { req = {}; }
    const id = url.pathname.split('/').pop();
    const player = players().find((p) => p.playerAccountId === id);
    if (player) {
      if (typeof req.displayName === 'string') player.displayName = req.displayName;
      player.phoneNumber = typeof req.phoneNumber === 'string' ? req.phoneNumber : '';
    }
    return json(player ?? {});
  }
  // Writes acknowledge with no content; unmatched reads return an empty list so list-driven screens
  // render their themed empty state instead of throwing.
  if (method !== 'GET') {
    return noContent();
  }
  return json([]);
}
