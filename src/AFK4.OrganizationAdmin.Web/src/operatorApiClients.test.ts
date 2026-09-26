import { describe, expect, it } from 'bun:test';
import {
  createOperatorApiClients,
  type CloseShiftRequest,
  type CreatePosSaleRequest,
  type EndSessionRequest,
  type ExtendSessionRequest,
  type OpenShiftRequest,
  type SettlePosSaleRequest,
  type StartGuestSessionRequest,
  type StartReservationSessionRequest,
  type TransferSessionRequest
} from './operatorApiClients';
import { PlatformApiClient } from './platformApi';
import { defaultWorkingHours } from './settings/club/workingHours';

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
  it('maps device rename and remove with exact request bodies', async () => {
    const { clients, calls } = createRecordedClients();
    await clients.devices.renameDevice(deviceId, { organizationId, displayName: 'VIP-01' });
    await clients.devices.removeDevice(deviceId, { organizationId, reason: 'retired' });
    expect(calls).toMatchObject([
      { method: 'POST', path: `/api/organizations/organization-id/devices/${deviceId}/rename`, body: { organizationId, displayName: 'VIP-01' } },
      { method: 'POST', path: `/api/organizations/organization-id/devices/${deviceId}/remove`, body: { organizationId, reason: 'retired' } }
    ]);
  });
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
      `GET /api/organizations/organization-id/branches/${branchId}/floor-map`,
      `GET /api/organizations/organization-id/branches/${branchId}/dashboard/summary?fromUtc=2026-05-21T00%3A00%3A00.000Z&toUtc=2026-05-21T23%3A59%3A59.000Z&limit=8`,
      `POST /api/organizations/organization-id/branches/${branchId}/sessions/start`,
      `POST /api/organizations/organization-id/sessions/${sessionId}/extend`,
      `POST /api/organizations/organization-id/sessions/${sessionId}/transfer`,
      `POST /api/organizations/organization-id/sessions/${sessionId}/end`
    ]);
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/sessions/start`)).toEqual(startRequest);
    expect(bodyOf(calls, `POST /api/organizations/organization-id/sessions/${sessionId}/end`)).toEqual(endRequest);
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
      `GET /api/organizations/organization-id/branches/${branchId}/pos/catalog`,
      `POST /api/organizations/organization-id/branches/${branchId}/inventory/stock-movements`,
      `GET /api/organizations/organization-id/branches/${branchId}/inventory/stock-movements?productId=77777777-7777-7777-7777-777777777777&limit=8`,
      `POST /api/organizations/organization-id/branches/${branchId}/pos/sales`,
      `POST /api/organizations/organization-id/pos/sales/${saleId}/refunds`,
      `POST /api/organizations/organization-id/pos/sales/${saleId}/void`,
      `GET /api/organizations/organization-id/pos/sales/${saleId}`,
      'GET /api/organizations/organization-id/receipts/11111111-1111-1111-1111-111111111111',
      `GET /api/organizations/organization-id/branches/${branchId}/players?query=Amir+K%26VIP&limit=20`,
      'POST /api/organizations/organization-id/players/12121212-1212-1212-1212-121212121212/packages/purchases',
      `POST /api/organizations/organization-id/branches/${branchId}/shifts/open`,
      `POST /api/organizations/organization-id/shifts/${shiftId}/close`,
      `GET /api/organizations/organization-id/branches/${branchId}/reports/sales/export.csv?fromUtc=2026-05-21T01%3A02%3A03.000Z&toUtc=2026-05-21T02%3A03%3A04.000Z&limit=50`
    ]);
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/inventory/stock-movements`)).toEqual(stockRequest);
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/pos/sales`)).toEqual(saleRequest);
    expect(bodyOf(calls, `POST /api/organizations/organization-id/pos/sales/${saleId}/refunds`)).toEqual(refundRequest);
    expect(bodyOf(calls, `POST /api/organizations/organization-id/pos/sales/${saleId}/void`)).toEqual(voidRequest);
    expect(bodyOf(calls, 'POST /api/organizations/organization-id/players/12121212-1212-1212-1212-121212121212/packages/purchases')).toEqual({
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
      `POST /api/organizations/organization-id/pos/sales/${saleId}/settlements`
    ]);
    expect(bodyOf(calls, `POST /api/organizations/organization-id/pos/sales/${saleId}/settlements`)).toEqual(request);
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
      playerAccountId: null,
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
      playerAccountId: null,
      seatId,
      customerName: 'Aziz Prime',
      phoneNumber: '+992900000001',
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
    await clients.reservations.noShow(reservationId, { organizationId, expectedVersion: 9 });
    await clients.reservations.startSession(reservationId, startSessionRequest);

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      `GET /api/organizations/organization-id/branches/${branchId}/reservations?fromUtc=2026-05-21T00%3A00%3A00.000Z&toUtc=2026-05-21T23%3A59%3A59.999Z&limit=40&state=confirmed`,
      `POST /api/organizations/organization-id/branches/${branchId}/reservations`,
      `PATCH /api/organizations/organization-id/reservations/${reservationId}`,
      `POST /api/organizations/organization-id/reservations/${reservationId}/confirm`,
      `POST /api/organizations/organization-id/reservations/${reservationId}/seat`,
      `POST /api/organizations/organization-id/reservations/${reservationId}/cancel`,
      `POST /api/organizations/organization-id/reservations/${reservationId}/no-show`,
      `POST /api/organizations/organization-id/reservations/${reservationId}/start-session`
    ]);
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/reservations`)).toEqual(createRequest);
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/reservations/${reservationId}`)).toEqual(updateRequest);
    expect(bodyOf(calls, `POST /api/organizations/organization-id/reservations/${reservationId}/confirm`)).toEqual({ organizationId, expectedVersion: 5 });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/reservations/${reservationId}/seat`)).toEqual({ organizationId, expectedVersion: 6 });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/reservations/${reservationId}/cancel`)).toEqual({ organizationId, reason: 'client called', expectedVersion: 7 });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/reservations/${reservationId}/no-show`)).toEqual({ organizationId, expectedVersion: 9 });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/reservations/${reservationId}/start-session`)).toEqual(startSessionRequest);
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
      newPassword: '456456'
    });
    await clients.settings.getBranchProfile(branchId);
    await clients.settings.updateBranchProfile(branchId, {
      organizationId,
      name: 'AFK4 Pilot',
      city: 'Dushanbe',
      description: null,
      address: null,
      phone: null,
      telegram: null,
      website: null,
      instagram: null,
      logoUrl: null,
      logoMediaId: null,
      coverImageUrl: null,
      coverMediaId: null,
      photos: [],
      latitude: null,
      longitude: null,
      timeZone: 'Asia/Dushanbe',
      locale: 'ru',
      workingHours: defaultWorkingHours()
    });
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
    await clients.settings.listProductCategories(branchId);
    await clients.settings.createProductCategory(branchId, {
      organizationId,
      name: 'Snacks',
      idempotencyKey: 'idem-category'
    });
    await clients.settings.updateProductCategory(branchId, '77777777-7777-7777-7777-777777777777', {
      organizationId,
      name: 'Снеки'
    });
    await clients.settings.updateProductCategory(branchId, '88888888-8888-8888-8888-888888888888', {
      organizationId,
      isActive: false
    });
    await clients.settings.reorderProductCategories(branchId, {
      organizationId,
      categoryIds: ['88888888-8888-8888-8888-888888888888', '77777777-7777-7777-7777-777777777777']
    });
    await clients.settings.createProduct(branchId, {
      organizationId,
      categoryId: '88888888-8888-8888-8888-888888888888',
      name: 'Energy Bar',
      sku: 'BAR-01',
      price: { currencyCode: 'TJS', minorUnits: 3550 },
      trackStock: true,
      allowNegativeStock: false,
      reorderThreshold: 5,
      availableInShell: true,
      featuredOnPcs: false,
      imageUrl: null,
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
    await clients.settings.getBranchSettings(branchId);
    await clients.settings.updateBranchSettings(branchId, {
      organizationId,
      requireManualDeviceApproval: true,
      preferredLocale: 'ru'
    });
    await clients.settings.assignDeviceSeat(branchId, deviceId, { organizationId, seatId });
    await clients.devices.listDevices(branchId);
    await clients.devices.listPendingDevices(branchId);
    await clients.devices.approveDevice(deviceId, { organizationId });
    await clients.devices.rejectDevice(deviceId, { organizationId, reason: 'Не наш ПК' });
    await clients.devices.dispatchDeviceCommand(deviceId, { type: 'lock', payload: { reason: 'operator' } });
    await clients.devices.listDeviceCommands(deviceId, { limit: 25 });
    await clients.devices.listBranchDeviceCommands(branchId, { limit: 50 });
    await clients.devices.getDeviceCommandStatus(deviceId, commandId);
    await clients.diagnostics.getDiagnostics(branchId);
    await clients.updates.getRolloutStatuses(branchId);
    await clients.updates.getPreference(branchId);
    await clients.updates.updatePreference(branchId, {
      organizationId,
      maintenanceWindowStart: '03:00:00',
      maintenanceWindowEnd: '05:00:00'
    });
    await clients.audit.search({
      branchId,
      action: 'session.end',
      outcome: 'success',
      targetType: 'session',
      limit: 25
    });

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      `GET /api/organizations/organization-id/branches/${branchId}/staff`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/profile`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/roles`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/state`,
      `POST /api/organizations/organization-id/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/password-reset`,
      `GET /api/organizations/organization-id/branches/${branchId}/profile`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/profile`,
      `POST /api/organizations/organization-id/branches/${branchId}/layout/zones`,
      `POST /api/organizations/organization-id/branches/${branchId}/layout/seats`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/layout/zones/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/layout/seats/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`,
      `GET /api/organizations/organization-id/branches/${branchId}/tariffs/options`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/tariffs/11111111-1111-1111-1111-111111111111`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/tariffs/11111111-1111-1111-1111-111111111111/versions/22222222-2222-2222-2222-222222222222`,
      `GET /api/organizations/organization-id/branches/${branchId}/packages/options`,
      `POST /api/organizations/organization-id/branches/${branchId}/packages`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/packages/abababab-abab-abab-abab-abababababab`,
      `GET /api/organizations/organization-id/branches/${branchId}/pos/categories`,
      `POST /api/organizations/organization-id/branches/${branchId}/pos/categories`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/pos/categories/77777777-7777-7777-7777-777777777777`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/pos/categories/88888888-8888-8888-8888-888888888888`,
      `POST /api/organizations/organization-id/branches/${branchId}/pos/categories/order`,
      `POST /api/organizations/organization-id/branches/${branchId}/pos/products`,
      `PATCH /api/organizations/organization-id/branches/${branchId}/pos/products/77777777-7777-7777-7777-777777777777`,
      `GET /api/organizations/organization-id/branches/${branchId}/settings`,
      `PUT /api/organizations/organization-id/branches/${branchId}/settings`,
      `POST /api/organizations/organization-id/branches/${branchId}/devices/${deviceId}/seat-assignment`,
      `GET /api/organizations/organization-id/branches/${branchId}/devices`,
      `GET /api/organizations/organization-id/branches/${branchId}/devices/pending`,
      `POST /api/organizations/organization-id/devices/${deviceId}/approve`,
      `POST /api/organizations/organization-id/devices/${deviceId}/reject`,
      `POST /api/organizations/organization-id/devices/${deviceId}/commands`,
      `GET /api/organizations/organization-id/devices/${deviceId}/commands?limit=25`,
      `GET /api/organizations/organization-id/branches/${branchId}/device-commands?limit=50`,
      `GET /api/organizations/organization-id/devices/${deviceId}/commands/${commandId}/status`,
      `GET /api/organizations/organization-id/branches/${branchId}/diagnostics`,
      `GET /api/organizations/organization-id/branches/${branchId}/updates/rollouts`,
      `GET /api/organizations/organization-id/branches/${branchId}/updates/preferences`,
      `PUT /api/organizations/organization-id/branches/${branchId}/updates/preferences`,
      `GET /api/organizations/organization-id/branches/${branchId}/audit?action=session.end&outcome=success&targetType=session&limit=25`
    ]);
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/profile`)).toEqual({ organizationId, userName: 'cashier2', displayName: 'Cashier Two' });
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/roles`)).toEqual({ organizationId, roleNames: ['technician'] });
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/state`)).toEqual({ organizationId, isActive: false });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/staff/77777777-7777-7777-7777-777777777777/password-reset`)).toEqual({ organizationId, newPassword: '456456' });
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/profile`)).toEqual({
      organizationId,
      name: 'AFK4 Pilot',
      city: 'Dushanbe',
      description: null,
      address: null,
      phone: null,
      telegram: null,
      website: null,
      instagram: null,
      logoUrl: null,
      logoMediaId: null,
      coverImageUrl: null,
      coverMediaId: null,
      photos: [],
      latitude: null,
      longitude: null,
      timeZone: 'Asia/Dushanbe',
      locale: 'ru',
      workingHours: defaultWorkingHours()
    });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/layout/zones`)).toEqual({ organizationId, name: 'Main', sortOrder: 10 });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/layout/seats`)).toEqual({ organizationId, zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', name: 'PC-01', sortOrder: 20 });
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/layout/zones/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb`)).toEqual({ organizationId, name: 'VIP', sortOrder: 30 });
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/layout/seats/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`)).toEqual({ organizationId, zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', name: 'VIP-01', sortOrder: 40 });
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/tariffs/11111111-1111-1111-1111-111111111111`)).toEqual({ organizationId, name: 'Standard Plus', isActive: false });
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/tariffs/11111111-1111-1111-1111-111111111111/versions/22222222-2222-2222-2222-222222222222`)).toMatchObject({ organizationId, pricePerMinuteMinorUnits: 75, isActive: false });
    expect(bodyOf(calls, `PUT /api/organizations/organization-id/branches/${branchId}/updates/preferences`)).toEqual({
      organizationId,
      maintenanceWindowStart: '03:00:00',
      maintenanceWindowEnd: '05:00:00'
    });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/packages`)).toMatchObject({ organizationId, name: 'Night 5h', includedSeconds: 18000 });
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/packages/abababab-abab-abab-abab-abababababab`)).toMatchObject({ organizationId, name: 'Night 6h', includedSeconds: 21600, isActive: false });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/pos/categories`)).toEqual({ organizationId, name: 'Snacks', idempotencyKey: 'idem-category' });
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/pos/categories/77777777-7777-7777-7777-777777777777`)).toEqual({ organizationId, name: 'Снеки' });
    // Скрытие идёт тем же маршрутом и без имени: пропущенное поле не должно затирать название.
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/pos/categories/88888888-8888-8888-8888-888888888888`)).toEqual({ organizationId, isActive: false });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/pos/categories/order`)).toEqual({
      organizationId,
      categoryIds: ['88888888-8888-8888-8888-888888888888', '77777777-7777-7777-7777-777777777777']
    });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/pos/products`)).toMatchObject({ organizationId, name: 'Energy Bar', sku: 'BAR-01' });
    expect(bodyOf(calls, `PATCH /api/organizations/organization-id/branches/${branchId}/pos/products/77777777-7777-7777-7777-777777777777`)).toMatchObject({ organizationId, name: 'Energy Bar Zero', sku: 'BAR-ZERO', isActive: false });
    expect(bodyOf(calls, `PUT /api/organizations/organization-id/branches/${branchId}/settings`)).toEqual({ organizationId, requireManualDeviceApproval: true, preferredLocale: 'ru' });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/devices/${deviceId}/approve`)).toEqual({ organizationId });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/devices/${deviceId}/reject`)).toEqual({ organizationId, reason: 'Не наш ПК' });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/devices/${deviceId}/commands`)).toEqual({ type: 'lock', payload: { reason: 'operator' } });
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
      `GET /api/organizations/organization-id/branches/${branchId}/money-actions`,
      `POST /api/organizations/organization-id/branches/${branchId}/money-actions/${requestId}/approve`,
      `POST /api/organizations/organization-id/branches/${branchId}/money-actions/${requestId}/reject`,
      `GET /api/organizations/organization-id/branches/${branchId}/audit?actorStaffUserId=3db1367b-88c6-4b1c-99c3-bcbb5f4d5134&minAmount=1000&maxAmount=5000&limit=50`
    ]);
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/money-actions/${requestId}/approve`)).toEqual({ decisionReason: null });
    expect(bodyOf(calls, `POST /api/organizations/organization-id/branches/${branchId}/money-actions/${requestId}/reject`)).toEqual({ decisionReason: 'Нет чека' });
  });

  it('maps layout delete clients with organization scoping', async () => {
    const { clients, calls } = createRecordedClients();

    await clients.settings.deleteSeat(branchId, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', organizationId);
    await clients.settings.deleteZone(branchId, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', organizationId);

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      `DELETE /api/organizations/organization-id/branches/${branchId}/layout/seats/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa?organizationId=${organizationId}`,
      `DELETE /api/organizations/organization-id/branches/${branchId}/layout/zones/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb?organizationId=${organizationId}`
    ]);
  });
});

interface RecordedCall {
  method: string;
  path: string;
  body: unknown;
}

/**
 * Тело единственного записанного запроса на этот маршрут.
 *
 * Раньше проверки адресовались номером в списке (`calls[19].body`). Любая вставка маршрута в
 * середину сценария сдвигала все последующие номера, и краснели чужие проверки, к правке отношения не
 * имевшие. Маршрут — то, что проверка на самом деле имеет в виду.
 *
 * Ровно один, а не «первый подходящий»: два запроса на один маршрут делают проверку двусмысленной,
 * и лучше сказать об этом вслух, чем молча взять один из двух.
 */
function bodyOf(calls: readonly RecordedCall[], route: string): unknown {
  const matches = calls.filter((call) => `${call.method} ${call.path}` === route);
  if (matches.length !== 1) {
    throw new Error(`Ожидался ровно один запрос «${route}», найдено ${matches.length}.`);
  }
  return matches[0].body;
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
    clients: createOperatorApiClients(api, 'organization-id')
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
  it('gets and updates organization loyalty settings', async () => {
    const calls: Array<{ method: string; path: string; body?: unknown }> = [];
    const apiFake = {
      get: async <T,>(path: string) => { calls.push({ method: 'GET', path }); return { topUpEnabled: false, topUpPercentBasisPoints: 0, shopEnabled: false, shopPercentBasisPoints: 0 } as T; },
      post: async <T,>(path: string, body: unknown) => { calls.push({ method: 'POST', path, body }); return body as T; },
      patch: async <T,>() => ({} as T)
    };
    const client = createLoyaltySettingsClient(apiFake as never);
    await client.get();
    const payload = {
      topUpEnabled: true, topUpPercentBasisPoints: 500,
      shopEnabled: false, shopPercentBasisPoints: 0,
      sessionEnabled: true, sessionPercentBasisPoints: 300,
      cashbackCapMinorUnits: 5000, minimumSourceMinorUnits: 1000
    };
    await client.update(payload);
    expect(calls).toEqual([
      { method: 'GET', path: 'loyalty-settings' },
      { method: 'POST', path: 'loyalty-settings', body: payload }
    ]);
  });
});
