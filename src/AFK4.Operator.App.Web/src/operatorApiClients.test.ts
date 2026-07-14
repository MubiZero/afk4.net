import { describe, expect, it } from 'bun:test';
import {
  createOperatorApiClients,
  type CloseShiftRequest,
  type CreatePosSaleRequest,
  type EndSessionRequest,
  type ExtendSessionRequest,
  type ManualPaymentRequest,
  type OpenShiftRequest,
  type SettlePosSaleRequest,
  type StartGuestSessionRequest,
  type StartReservationSessionRequest,
  type TransferSessionRequest
} from './operatorApiClients';
import { PlatformApiClient } from './platformApi';

const branchId = 'acfc0212-967f-4d84-94be-9003387b09c2';
const organizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';
const seatId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const sessionId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const shiftId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
const saleId = 'dddddddd-dddd-dddd-dddd-dddddddddddd';
const deviceId = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee';
const commandId = 'ffffffff-ffff-ffff-ffff-ffffffffffff';
const reservationId = '99999999-9999-9999-9999-999999999999';

describe('operator API clients', () => {
  it('maps floor-map and session clients to current backend routes', async () => {
    const { clients, calls } = createRecordedClients();
    const startRequest: StartGuestSessionRequest = {
      organizationId,
      seatId,
      durationMinutes: 60,
      tariffRuleVersionId: 'standard-v1',
      idempotencyKey: 'idem-start',
      billingMode: 'guest_no_ledger'
    };
    const extendRequest: ExtendSessionRequest = {
      additionalMinutes: 15,
      tariffRuleVersionId: 'standard-v1',
      idempotencyKey: 'idem-extend'
    };
    const transferRequest: TransferSessionRequest = {
      targetSeatId: seatId,
      idempotencyKey: 'idem-transfer'
    };
    const endRequest: EndSessionRequest = {
      reason: 'operator',
      idempotencyKey: 'idem-end'
    };

    await clients.floorMap.getFloorMap(branchId);
    await clients.dashboard.getSummary(branchId, {
      fromUtc: '2026-05-21T00:00:00.000Z',
      toUtc: '2026-05-21T23:59:59.000Z',
      limit: 8
    });
    await clients.sessions.startGuestSession(branchId, startRequest);
    await clients.sessions.extendSession(sessionId, extendRequest);
    await clients.sessions.transferSession(sessionId, transferRequest);
    await clients.sessions.endSession(sessionId, endRequest);

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      `GET /api/branches/${branchId}/floor-map`,
      `GET /api/branches/${branchId}/dashboard/summary?fromUtc=2026-05-21T00%3A00%3A00.000Z&toUtc=2026-05-21T23%3A59%3A59.000Z&limit=8`,
      `POST /api/branches/${branchId}/sessions/start`,
      `POST /api/sessions/${sessionId}/extend`,
      `POST /api/sessions/${sessionId}/transfer`,
      `POST /api/sessions/${sessionId}/end`
    ]);
    expect(calls[2].body).toEqual(startRequest);
    expect(calls[5].body).toEqual(endRequest);
  });

  it('maps POS, player, and shift clients including query and CSV routes', async () => {
    const { clients, calls } = createRecordedClients();
    const saleRequest: CreatePosSaleRequest = {
      organizationId,
      shiftId,
      idempotencyKey: 'idem-sale',
      lines: [],
      playerAccountId: '12121212-1212-1212-1212-121212121212'
    };
    const paymentRequest: ManualPaymentRequest = {
      organizationId,
      paymentMethod: 'cash',
      amount: { currencyCode: 'TJS', minorUnits: 1200 },
      note: 'cash',
      idempotencyKey: 'idem-pay'
    };
    const refundRequest = {
      organizationId,
      reason: 'customer refund',
      idempotencyKey: 'idem-refund'
    };
    const voidRequest = {
      organizationId,
      reason: 'mistaken draft',
      idempotencyKey: 'idem-void'
    };
    const stockRequest = {
      organizationId,
      productId: '77777777-7777-7777-7777-777777777777',
      movementType: 'purchase',
      quantityDelta: 10,
      unitCost: { currencyCode: 'TJS', minorUnits: 0 },
      reason: 'initial stock',
      idempotencyKey: 'idem-stock'
    };
    const openShiftRequest: OpenShiftRequest = {
      organizationId,
      startingCash: { currencyCode: 'TJS', minorUnits: 50000 },
      openingNote: 'morning',
      idempotencyKey: 'idem-open'
    };
    const closeShiftRequest: CloseShiftRequest = {
      organizationId,
      countedCash: { currencyCode: 'TJS', minorUnits: 52000 },
      closingNote: 'evening',
      idempotencyKey: 'idem-close'
    };

    await clients.pos.getCatalog(branchId);
    await clients.inventory.createStockMovement(branchId, stockRequest);
    await clients.inventory.getStockMovements(branchId, {
      productId: stockRequest.productId,
      limit: 8
    });
    await clients.pos.createSale(branchId, saleRequest);
    await clients.pos.paySaleManual(saleId, paymentRequest);
    await clients.pos.refundSale(saleId, refundRequest);
    await clients.pos.voidSale(saleId, voidRequest);
    await clients.pos.getSale(saleId);
    await clients.pos.getReceipt('11111111-1111-1111-1111-111111111111');
    await clients.players.searchPlayers(branchId, 'Amir K&VIP', 20);
    await clients.players.purchasePackage('12121212-1212-1212-1212-121212121212', {
      organizationId,
      packageDefinitionId: 'abababab-abab-abab-abab-abababababab',
      idempotencyKey: 'idem-package'
    });
    await clients.shifts.openShift(branchId, openShiftRequest);
    await clients.shifts.closeShift(shiftId, closeShiftRequest);
    await clients.shifts.exportSalesReportCsv(branchId, {
      fromUtc: new Date('2026-05-21T01:02:03.000Z'),
      toUtc: '2026-05-21T02:03:04.000Z',
      limit: 50
    });

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      `GET /api/branches/${branchId}/pos/catalog`,
      `POST /api/branches/${branchId}/inventory/stock-movements`,
      `GET /api/branches/${branchId}/inventory/stock-movements?productId=77777777-7777-7777-7777-777777777777&limit=8`,
      `POST /api/branches/${branchId}/pos/sales`,
      `POST /api/pos/sales/${saleId}/payments/manual`,
      `POST /api/pos/sales/${saleId}/refunds`,
      `POST /api/pos/sales/${saleId}/void`,
      `GET /api/pos/sales/${saleId}`,
      'GET /api/receipts/11111111-1111-1111-1111-111111111111',
      `GET /api/branches/${branchId}/players?query=Amir+K%26VIP&limit=20`,
      'POST /api/players/12121212-1212-1212-1212-121212121212/packages/purchases',
      `POST /api/branches/${branchId}/shifts/open`,
      `POST /api/shifts/${shiftId}/close`,
      `GET /api/branches/${branchId}/reports/sales/export.csv?fromUtc=2026-05-21T01%3A02%3A03.000Z&toUtc=2026-05-21T02%3A03%3A04.000Z&limit=50`
    ]);
    expect(calls[1].body).toEqual(stockRequest);
    expect(calls[3].body).toEqual(saleRequest);
    expect(calls[4].body).toEqual(paymentRequest);
    expect(calls[5].body).toEqual(refundRequest);
    expect(calls[6].body).toEqual(voidRequest);
    expect(calls[10].body).toEqual({
      organizationId,
      packageDefinitionId: 'abababab-abab-abab-abab-abababababab',
      idempotencyKey: 'idem-package'
    });
  });

  it('posts multipart POS settlements to the settlement route', async () => {
    const { clients, calls } = createRecordedClients();
    const request: SettlePosSaleRequest = {
      organizationId,
      payments: [
        { paymentMethod: 'wallet', amount: { currencyCode: 'TJS', minorUnits: 4000 } },
        { paymentMethod: 'cash', amount: { currencyCode: 'TJS', minorUnits: 6000 } }
      ],
      note: 'operator POS checkout',
      idempotencyKey: 'pos-settle-1'
    };

    await clients.pos.settleSale(saleId, request);

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      `POST /api/pos/sales/${saleId}/settlements`
    ]);
    expect(calls[0].body).toEqual(request);
  });

  it('returns null for no current shift', async () => {
    const { clients } = createRecordedClients((url) => {
      if (url.pathname.endsWith('/shifts/current')) {
        return new Response('', { status: 404, statusText: 'Not Found' });
      }

      return jsonResponse({ ok: true });
    });

    await expect(clients.shifts.getCurrentShift(branchId)).resolves.toBeNull();
  });

  it('maps reservation clients to booking backend routes', async () => {
    const { clients, calls } = createRecordedClients();
    const createRequest = {
      organizationId,
      seatId,
      customerName: 'Aziz P.',
      phoneNumber: '+992900000001',
      startsAtUtc: '2026-05-21T16:00:00.000Z',
      durationMinutes: 60,
      source: 'operator',
      note: 'front desk'
    };
    const updateRequest = {
      organizationId,
      expectedVersion: 4,
      seatId,
      customerName: 'Aziz Prime',
      startsAtUtc: '2026-05-21T17:00:00.000Z',
      durationMinutes: 90,
      source: 'operator',
      note: 'moved'
    };
    const startSessionRequest: StartReservationSessionRequest = {
      organizationId,
      expectedVersion: 8,
      tariffRuleVersionId: 'standard-v1',
      idempotencyKey: 'reservation-start-1',
      durationMode: 'fixed',
      durationMinutes: 60,
      billingMode: ''
    };

    await clients.reservations.search(branchId, {
      fromUtc: '2026-05-21T00:00:00.000Z',
      toUtc: '2026-05-21T23:59:59.999Z',
      limit: 40,
      state: 'confirmed'
    });
    await clients.reservations.create(branchId, createRequest);
    await clients.reservations.update(reservationId, updateRequest);
    await clients.reservations.confirm(reservationId, { organizationId, expectedVersion: 5 });
    await clients.reservations.seat(reservationId, { organizationId, expectedVersion: 6 });
    await clients.reservations.cancel(reservationId, { organizationId, reason: 'client called', expectedVersion: 7 });
    await clients.reservations.startSession(reservationId, startSessionRequest);

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      `GET /api/branches/${branchId}/reservations?fromUtc=2026-05-21T00%3A00%3A00.000Z&toUtc=2026-05-21T23%3A59%3A59.999Z&limit=40&state=confirmed`,
      `POST /api/branches/${branchId}/reservations`,
      `PATCH /api/reservations/${reservationId}`,
      `POST /api/reservations/${reservationId}/confirm`,
      `POST /api/reservations/${reservationId}/seat`,
      `POST /api/reservations/${reservationId}/cancel`,
      `POST /api/reservations/${reservationId}/start-session`
    ]);
    expect(calls[1].body).toEqual(createRequest);
    expect(calls[2].body).toEqual(updateRequest);
    expect(calls[3].body).toEqual({ organizationId, expectedVersion: 5 });
    expect(calls[4].body).toEqual({ organizationId, expectedVersion: 6 });
    expect(calls[5].body).toEqual({ organizationId, reason: 'client called', expectedVersion: 7 });
    expect(calls[6].body).toEqual(startSessionRequest);
  });

  it('maps settings, device, diagnostics, updates, and audit clients', async () => {
    const { clients, calls } = createRecordedClients();

    await clients.settings.getStaffUsers(branchId);
    await clients.settings.updateStaffUserProfile(branchId, '77777777-7777-7777-7777-777777777777', {
      organizationId,
      userName: 'cashier2',
      displayName: 'Cashier Two'
    });
    await clients.settings.updateStaffUserRoles(branchId, '77777777-7777-7777-7777-777777777777', {
      organizationId,
      roleNames: ['technician']
    });
    await clients.settings.updateStaffUserState(branchId, '77777777-7777-7777-7777-777777777777', {
      organizationId,
      isActive: false
    });
    await clients.settings.resetStaffUserPassword(branchId, '77777777-7777-7777-7777-777777777777', {
      organizationId,
      newPassword: 'ChangeMe456!'
    });
    await clients.settings.getBranchProfile(branchId);
    await clients.settings.updateBranchProfile(branchId, { organizationId, name: 'AFK4 Pilot', city: 'Dushanbe' });
    await clients.settings.createZone(branchId, { organizationId, name: 'Main', sortOrder: 10 });
    await clients.settings.createSeat(branchId, { organizationId, zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', name: 'PC-01', sortOrder: 20 });
    await clients.settings.updateZone(branchId, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', { organizationId, name: 'VIP', sortOrder: 30 });
    await clients.settings.updateSeat(branchId, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', {
      organizationId,
      zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      name: 'VIP-01',
      sortOrder: 40
    });
    await clients.settings.getTariffOptions(branchId);
    await clients.settings.updateTariff(branchId, '11111111-1111-1111-1111-111111111111', {
      organizationId,
      name: 'Standard Plus',
      isActive: false
    });
    await clients.settings.updateTariffVersion(
      branchId,
      '11111111-1111-1111-1111-111111111111',
      '22222222-2222-2222-2222-222222222222',
      {
        organizationId,
        currencyCode: 'TJS',
        pricePerMinuteMinorUnits: 75,
        minimumBillableMinutes: 20,
        roundingIncrementMinutes: 10,
        effectiveFromUtc: '2026-05-22T10:00:00.000Z',
        isActive: false
      });
    await clients.settings.getPackageOptions(branchId);
    await clients.settings.createPackageDefinition(branchId, {
      organizationId,
      name: 'Night 5h',
      price: { currencyCode: 'TJS', minorUnits: 25000 },
      includedSeconds: 18000,
      bonusSeconds: 1800,
      expiresAfterDays: 30,
      idempotencyKey: 'idem-package-definition'
    });
    await clients.settings.updatePackageDefinition(branchId, 'abababab-abab-abab-abab-abababababab', {
      organizationId,
      name: 'Night 6h',
      price: { currencyCode: 'TJS', minorUnits: 30000 },
      includedSeconds: 21600,
      bonusSeconds: 2400,
      expiresAfterDays: 45,
      isActive: false
    });
    await clients.settings.createProductCategory(branchId, {
      organizationId,
      name: 'Snacks',
      idempotencyKey: 'idem-category'
    });
    await clients.settings.createProduct(branchId, {
      organizationId,
      categoryId: '88888888-8888-8888-8888-888888888888',
      name: 'Energy Bar',
      sku: 'BAR-01',
      price: { currencyCode: 'TJS', minorUnits: 3550 },
      trackStock: true,
      allowNegativeStock: false,
      idempotencyKey: 'idem-product'
    });
    await clients.settings.updateProduct(branchId, '77777777-7777-7777-7777-777777777777', {
      organizationId,
      categoryId: '88888888-8888-8888-8888-888888888888',
      name: 'Energy Bar Zero',
      sku: 'BAR-ZERO',
      price: { currencyCode: 'TJS', minorUnits: 3950 },
      trackStock: true,
      allowNegativeStock: true,
      isActive: false
    });
    await clients.settings.assignDeviceSeat(branchId, deviceId, { organizationId, seatId });
    await clients.devices.listDevices(branchId);
    await clients.devices.createEnrollmentCode(branchId, organizationId, 900);
    await clients.devices.dispatchDeviceCommand(deviceId, { type: 'lock', payload: { reason: 'operator' } });
    await clients.devices.listDeviceCommands(deviceId, { limit: 25 });
    await clients.devices.listBranchDeviceCommands(branchId, { limit: 50 });
    await clients.devices.getDeviceCommandStatus(deviceId, commandId);
    await clients.diagnostics.getDiagnostics(branchId);
    await clients.updates.getRolloutStatuses(branchId);
    await clients.updates.changeRolloutState(branchId, '99999999-9999-9999-9999-999999999999', {
      organizationId,
      state: 'paused'
    });
    await clients.audit.search({
      branchId,
      action: 'session.end',
      outcome: 'success',
      targetType: 'session',
      limit: 25
    });

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      `GET /api/branches/${branchId}/staff`,
      `PATCH /api/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/profile`,
      `PATCH /api/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/roles`,
      `PATCH /api/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/state`,
      `POST /api/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/password-reset`,
      `GET /api/branches/${branchId}/profile`,
      `PATCH /api/branches/${branchId}/profile`,
      `POST /api/branches/${branchId}/layout/zones`,
      `POST /api/branches/${branchId}/layout/seats`,
      `PATCH /api/branches/${branchId}/layout/zones/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb`,
      `PATCH /api/branches/${branchId}/layout/seats/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`,
      `GET /api/branches/${branchId}/tariffs/options`,
      `PATCH /api/branches/${branchId}/tariffs/11111111-1111-1111-1111-111111111111`,
      `PATCH /api/branches/${branchId}/tariffs/11111111-1111-1111-1111-111111111111/versions/22222222-2222-2222-2222-222222222222`,
      `GET /api/branches/${branchId}/packages/options`,
      `POST /api/branches/${branchId}/packages`,
      `PATCH /api/branches/${branchId}/packages/abababab-abab-abab-abab-abababababab`,
      `POST /api/branches/${branchId}/pos/categories`,
      `POST /api/branches/${branchId}/pos/products`,
      `PATCH /api/branches/${branchId}/pos/products/77777777-7777-7777-7777-777777777777`,
      `POST /api/branches/${branchId}/devices/${deviceId}/seat-assignment`,
      `GET /api/branches/${branchId}/devices`,
      `POST /api/branches/${branchId}/device-enrollment-codes`,
      `POST /api/devices/${deviceId}/commands`,
      `GET /api/devices/${deviceId}/commands?limit=25`,
      `GET /api/branches/${branchId}/device-commands?limit=50`,
      `GET /api/devices/${deviceId}/commands/${commandId}/status`,
      `GET /api/branches/${branchId}/diagnostics`,
      `GET /api/branches/${branchId}/updates/rollouts`,
      `POST /api/branches/${branchId}/updates/rollouts/99999999-9999-9999-9999-999999999999/state`,
      `GET /api/branches/${branchId}/audit?action=session.end&outcome=success&targetType=session&limit=25`
    ]);
    expect(calls[1].body).toEqual({ organizationId, userName: 'cashier2', displayName: 'Cashier Two' });
    expect(calls[2].body).toEqual({ organizationId, roleNames: ['technician'] });
    expect(calls[3].body).toEqual({ organizationId, isActive: false });
    expect(calls[4].body).toEqual({ organizationId, newPassword: 'ChangeMe456!' });
    expect(calls[6].body).toEqual({ organizationId, name: 'AFK4 Pilot', city: 'Dushanbe' });
    expect(calls[7].body).toEqual({ organizationId, name: 'Main', sortOrder: 10 });
    expect(calls[8].body).toEqual({ organizationId, zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', name: 'PC-01', sortOrder: 20 });
    expect(calls[9].body).toEqual({ organizationId, name: 'VIP', sortOrder: 30 });
    expect(calls[10].body).toEqual({ organizationId, zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', name: 'VIP-01', sortOrder: 40 });
    expect(calls[12].body).toEqual({ organizationId, name: 'Standard Plus', isActive: false });
    expect(calls[13].body).toMatchObject({ organizationId, pricePerMinuteMinorUnits: 75, isActive: false });
    expect(calls[15].body).toMatchObject({ organizationId, name: 'Night 5h', includedSeconds: 18000 });
    expect(calls[16].body).toMatchObject({ organizationId, name: 'Night 6h', includedSeconds: 21600, isActive: false });
    expect(calls[17].body).toEqual({ organizationId, name: 'Snacks', idempotencyKey: 'idem-category' });
    expect(calls[18].body).toMatchObject({ organizationId, name: 'Energy Bar', sku: 'BAR-01' });
    expect(calls[19].body).toMatchObject({ organizationId, name: 'Energy Bar Zero', sku: 'BAR-ZERO', isActive: false });
    expect(calls[22].body).toEqual({ organizationId, expiresInSeconds: 900 });
    expect(calls[23].body).toEqual({ type: 'lock', payload: { reason: 'operator' } });
  });

  it('maps money-action review endpoints and audit amount filters', async () => {
    const { clients, calls } = createRecordedClients();
    const requestId = '77777777-7777-7777-7777-777777777777';

    await clients.moneyActions.listPending(branchId);
    await clients.moneyActions.approve(branchId, requestId, { decisionReason: null });
    await clients.moneyActions.reject(branchId, requestId, { decisionReason: 'Нет чека' });
    await clients.audit.search({
      branchId,
      actorStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
      minAmount: 1000,
      maxAmount: 5000,
      limit: 50
    });

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      `GET /api/branches/${branchId}/money-actions`,
      `POST /api/branches/${branchId}/money-actions/${requestId}/approve`,
      `POST /api/branches/${branchId}/money-actions/${requestId}/reject`,
      `GET /api/branches/${branchId}/audit?actorStaffUserId=3db1367b-88c6-4b1c-99c3-bcbb5f4d5134&minAmount=1000&maxAmount=5000&limit=50`
    ]);
    expect(calls[1].body).toEqual({ decisionReason: null });
    expect(calls[2].body).toEqual({ decisionReason: 'Нет чека' });
  });

  it('maps layout delete clients with organization scoping', async () => {
    const { clients, calls } = createRecordedClients();

    await clients.settings.deleteSeat(branchId, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', organizationId);
    await clients.settings.deleteZone(branchId, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', organizationId);

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      `DELETE /api/branches/${branchId}/layout/seats/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa?organizationId=${organizationId}`,
      `DELETE /api/branches/${branchId}/layout/zones/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb?organizationId=${organizationId}`
    ]);
  });
});

interface RecordedCall {
  method: string;
  path: string;
  body: unknown;
}

function createRecordedClients(respond?: (url: URL, init: RequestInit) => Response) {
  const calls: RecordedCall[] = [];
  const fetchImpl = async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input));
    calls.push({
      method: init?.method ?? 'GET',
      path: `${url.pathname}${url.search}`,
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined
    });

    if (url.pathname.endsWith('.csv')) {
      return new Response('csv', { status: 200 });
    }

    return respond?.(url, init ?? {}) ?? jsonResponse({ ok: true });
  };
  const api = new PlatformApiClient({
    baseUrl: 'https://afk4.staging.mubi.dev/',
    getAccessToken: () => 'access-token',
    fetchImpl
  });

  return {
    calls,
    clients: createOperatorApiClients(api)
  };
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: {
      'Content-Type': 'application/json'
    }
  });
}

import { createLoyaltySettingsClient } from './operatorApiClients';

describe('createLoyaltySettingsClient', () => {
  it('gets and updates /api/owner/loyalty-settings', async () => {
    const calls: Array<{ method: string; path: string; body?: unknown }> = [];
    const apiFake = {
      get: async <T,>(path: string) => { calls.push({ method: 'GET', path }); return { topUpEnabled: false, topUpPercentBasisPoints: 0, shopEnabled: false, shopPercentBasisPoints: 0 } as T; },
      post: async <T,>(path: string, body: unknown) => { calls.push({ method: 'POST', path, body }); return body as T; },
      patch: async <T,>() => ({} as T)
    };
    const client = createLoyaltySettingsClient(apiFake as never);
    await client.get();
    await client.update({ topUpEnabled: true, topUpPercentBasisPoints: 500, shopEnabled: false, shopPercentBasisPoints: 0 });
    expect(calls).toEqual([
      { method: 'GET', path: '/api/owner/loyalty-settings' },
      { method: 'POST', path: '/api/owner/loyalty-settings', body: { topUpEnabled: true, topUpPercentBasisPoints: 500, shopEnabled: false, shopPercentBasisPoints: 0 } }
    ]);
  });
});
