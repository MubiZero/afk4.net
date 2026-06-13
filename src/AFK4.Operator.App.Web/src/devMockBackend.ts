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
    branchName: 'AFK4 Dushanbe · зал A',
    seats: [
      { seatId: 'a1', seatName: 'PC-01', zoneId: 'z-a', zoneName: 'Зал A', sortOrder: 10, state: 'Active', deviceId: 'd1', deviceName: 'PC-01', isDeviceOnline: true, isDeviceLocked: false, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: 's1', remainingSeconds: 2580 },
      { seatId: 'a2', seatName: 'PC-02', zoneId: 'z-a', zoneName: 'Зал A', sortOrder: 20, state: 'Ready', deviceId: 'd2', deviceName: 'PC-02', isDeviceOnline: true, isDeviceLocked: false, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: null, remainingSeconds: null },
      { seatId: 'a3', seatName: 'PC-03', zoneId: 'z-a', zoneName: 'Зал A', sortOrder: 30, state: 'Warning', deviceId: 'd3', deviceName: 'PC-03', isDeviceOnline: true, isDeviceLocked: false, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: 's3', remainingSeconds: 240 },
      { seatId: 'a4', seatName: 'PC-04', zoneId: 'z-a', zoneName: 'Зал A', sortOrder: 40, state: 'Blocking', deviceId: 'd4', deviceName: 'PC-04', isDeviceOnline: false, isDeviceLocked: true, lastHeartbeatAtUtc: '2026-05-21T09:40:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: null, remainingSeconds: null },
      { seatId: 'b1', seatName: 'VIP-01', zoneId: 'z-vip', zoneName: 'VIP', sortOrder: 10, state: 'Active', deviceId: 'd5', deviceName: 'VIP-01', isDeviceOnline: true, isDeviceLocked: false, lastHeartbeatAtUtc: '2026-05-21T10:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: 's5', remainingSeconds: 5400 },
      { seatId: 'b2', seatName: 'VIP-02', zoneId: 'z-vip', zoneName: 'VIP', sortOrder: 20, state: 'Service', deviceId: 'd6', deviceName: 'VIP-02', isDeviceOnline: false, isDeviceLocked: false, lastHeartbeatAtUtc: '2026-05-21T08:00:00Z', agentVersion: '0.4', shellVersion: '0.4', activeSessionId: null, remainingSeconds: null }
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

function reservation() {
  return {
    reservationId: 'r1', organizationId: ORG, branchId: BRANCH, playerAccountId: null,
    seatId: 'a2', seatName: 'PC-02', zoneName: 'Зал A', customerName: 'Азиз П.', phoneNumber: '+992900000001',
    startsAtUtc: '2026-05-21T16:00:00Z', endsAtUtc: '2026-05-21T17:00:00Z', durationMinutes: 60,
    state: 'pending', source: 'online', note: 'онлайн-заявка', createdAtUtc: '2026-05-21T10:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:00Z', cancelledAtUtc: null, cancelReason: ''
  };
}

// Route a platform request to a fixture. Returns null when nothing matches, so the caller can apply
// a safe default.
function route(pathname: string, method: string): unknown | undefined {
  if (pathname.endsWith('/floor-map')) return floorMap();
  if (pathname.endsWith('/dashboard/summary')) return dashboardSummary();
  if (pathname.endsWith('/shifts/current')) return currentShift();
  if (pathname.endsWith('/pos/catalog')) return [posProduct()];
  if (pathname.endsWith('/reservations') && method === 'GET') return { reservations: [reservation()], limit: 40 };
  if (pathname.endsWith('/inventory/stock-movements') && method === 'GET') return [];
  if (pathname.endsWith('/commands') && method === 'GET') return [];
  return undefined;
}

export async function devMockFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const url = new URL(String(input));
  const method = init?.method ?? 'GET';
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
