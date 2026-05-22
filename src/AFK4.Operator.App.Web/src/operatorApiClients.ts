import { PlatformApiClient, type QueryParams } from './platformApi';

export type Guid = string;

export interface MoneyDto {
  currencyCode: string;
  minorUnits: number;
}

export interface SeatStatusDto {
  seatId: Guid;
  seatName: string;
  zoneId: Guid;
  zoneName: string;
  sortOrder: number;
  state: string;
  deviceId?: Guid | null;
  deviceName?: string | null;
  isDeviceOnline?: boolean | null;
  isDeviceLocked?: boolean | null;
  lastHeartbeatAtUtc?: string | null;
  agentVersion?: string | null;
  shellVersion?: string | null;
  activeSessionId?: Guid | null;
  remainingSeconds?: number | null;
}

export interface FloorMapDto {
  branchId: Guid;
  branchName: string;
  seats: SeatStatusDto[];
}

export interface StartGuestSessionRequest {
  organizationId: Guid;
  seatId: Guid;
  durationMinutes: number;
  tariffRuleVersionId: string;
  idempotencyKey: string;
  playerAccountId?: Guid | null;
  billingMode?: string;
  tariffVersionId?: Guid | null;
  playerPackageId?: Guid | null;
}

export interface ExtendSessionRequest {
  additionalMinutes: number;
  tariffRuleVersionId: string;
  idempotencyKey: string;
  playerAccountId?: Guid | null;
  billingMode?: string;
  tariffVersionId?: Guid | null;
  playerPackageId?: Guid | null;
}

export interface TransferSessionRequest {
  targetSeatId: Guid;
  idempotencyKey: string;
}

export interface EndSessionRequest {
  reason: string;
  idempotencyKey: string;
}

export interface SessionCommandResponse {
  idempotencyKey: string;
  session: Record<string, unknown>;
  deviceCommands: Array<Record<string, unknown>>;
}

export interface PosSaleLineDto {
  productId: Guid;
  quantity: number;
  unitPrice: MoneyDto;
}

export interface CreatePosSaleRequest {
  organizationId: Guid;
  shiftId: Guid;
  lines: PosSaleLineDto[];
  idempotencyKey: string;
  playerAccountId?: Guid | null;
}

export interface ManualPaymentRequest {
  organizationId: Guid;
  paymentMethod: string;
  amount: MoneyDto;
  note: string;
  idempotencyKey: string;
}

export interface RefundPosSaleRequest {
  organizationId: Guid;
  reason: string;
  idempotencyKey: string;
}

export interface VoidPosSaleRequest {
  organizationId: Guid;
  reason: string;
  idempotencyKey: string;
}

export interface PosProductDto extends Record<string, unknown> {
  productId: Guid;
  name: string;
  price: MoneyDto;
}

export type PosSaleDto = Record<string, unknown>;
export type ReceiptDto = Record<string, unknown>;
export type PlayerSearchResultDto = Record<string, unknown>;
export type PlayerAccountDto = Record<string, unknown>;
export type WalletSummaryDto = Record<string, unknown>;
export type PlayerPackageDto = Record<string, unknown>;
export type ShiftDto = Record<string, unknown>;
export type CashMovementDto = Record<string, unknown>;
export type ReportResultDto = Record<string, unknown>;
export type OperatorDashboardSummaryDto = Record<string, unknown>;
export type ReservationDto = Record<string, unknown>;
export type ReservationSearchResultDto = Record<string, unknown>;
export type StaffUserDto = Record<string, unknown>;
export type BranchProfileDto = Record<string, unknown>;
export type ZoneDto = Record<string, unknown>;
export type SeatDto = Record<string, unknown>;
export type TariffDto = Record<string, unknown>;
export type TariffVersionDto = Record<string, unknown>;
export type TariffOptionDto = Record<string, unknown>;
export type PackageOptionDto = Record<string, unknown>;
export type PackageDefinitionDto = Record<string, unknown>;
export type PosProductCategoryDto = Record<string, unknown>;
export type StockMovementDto = Record<string, unknown>;
export type DeviceSeatAssignmentDto = Record<string, unknown>;
export type DeviceEnrollmentCodeDto = Record<string, unknown>;
export type DeviceCommandDto = Record<string, unknown>;
export type DeviceCommandStatusDto = Record<string, unknown>;
export type DeviceDetailDto = Record<string, unknown>;
export type RotateDeviceCredentialResponse = Record<string, unknown>;
export type RevokeDeviceCredentialResponse = Record<string, unknown>;
export type BranchDiagnosticsDto = Record<string, unknown>;
export type UpdatePackageDto = Record<string, unknown>;
export type UpdateRolloutDto = Record<string, unknown>;
export type UpdateRolloutStatusDto = Record<string, unknown>;
export type AuditSearchResultDto = Record<string, unknown>;

export interface CreatePlayerAccountRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface TopUpWalletRequest extends Record<string, unknown> {
  organizationId: Guid;
  amount: MoneyDto;
  idempotencyKey: string;
}

export interface PayDebtRequest extends Record<string, unknown> {
  organizationId: Guid;
  amount: MoneyDto;
  idempotencyKey: string;
}

export interface PurchasePackageRequest extends Record<string, unknown> {
  organizationId: Guid;
  packageDefinitionId: Guid;
  idempotencyKey: string;
}

export interface OpenShiftRequest {
  organizationId: Guid;
  startingCash: MoneyDto;
  openingNote: string;
  idempotencyKey: string;
}

export interface RecordCashMovementRequest {
  organizationId: Guid;
  movementType: string;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

export interface CloseShiftRequest {
  organizationId: Guid;
  countedCash: MoneyDto;
  closingNote: string;
  idempotencyKey: string;
}

export interface ReportQuery {
  fromUtc?: string | Date | null;
  toUtc?: string | Date | null;
  limit?: number | null;
}

export type DashboardSummaryQuery = ReportQuery;

export type StockMovementSearchQuery = ReportQuery & {
  productId?: Guid | null;
};

export type ReservationSearchQuery = ReportQuery & {
  state?: string | null;
  source?: string | null;
};

export interface CreateReservationRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface UpdateReservationRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface ConfirmReservationRequest {
  organizationId: Guid;
}

export interface SeatReservationRequest {
  organizationId: Guid;
}

export interface CancelReservationRequest {
  organizationId: Guid;
  reason: string;
}

export interface CreateStaffUserRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface UpdateStaffUserRolesRequest extends Record<string, unknown> {
  organizationId: Guid;
  roleNames: string[];
}

export interface UpdateBranchProfileRequest extends Record<string, unknown> {
  organizationId: Guid;
  name: string;
  city: string;
}

export interface CreateZoneRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateSeatRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateTariffRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateTariffVersionRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreatePackageDefinitionRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface UpdatePackageDefinitionRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateProductCategoryRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateProductRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface UpdateProductRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateStockMovementRequest extends Record<string, unknown> {
  organizationId: Guid;
  productId: Guid;
  movementType: string;
  quantityDelta: number;
  unitCost: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

export interface AssignDeviceSeatRequest extends Record<string, unknown> {
  organizationId: Guid;
  seatId: Guid;
}

export interface DispatchDeviceCommandRequest {
  type: string;
  payload: Record<string, string>;
}

export interface CreateUpdatePackageRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface UpdatePackageStateChangeRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateUpdateRolloutRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface UpdateRolloutStateChangeRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface AuditSearchRequest {
  branchId: Guid;
  action?: string | null;
  outcome?: string | null;
  targetType?: string | null;
  fromUtc?: string | Date | null;
  toUtc?: string | Date | null;
  limit?: number | null;
}

export function createOperatorApiClients(api: PlatformApiClient) {
  return {
    floorMap: createFloorMapClient(api),
    sessions: createSessionClient(api),
    pos: createPosClient(api),
    players: createPlayerClient(api),
    dashboard: createDashboardClient(api),
    reservations: createReservationClient(api),
    shifts: createShiftClient(api),
    settings: createSettingsClient(api),
    inventory: createInventoryClient(api),
    devices: createDeviceClient(api),
    diagnostics: createDiagnosticsClient(api),
    updates: createUpdateClient(api),
    audit: createAuditClient(api)
  };
}

export function createFloorMapClient(api: PlatformApiClient) {
  return {
    getFloorMap(branchId: Guid): Promise<FloorMapDto> {
      return api.get<FloorMapDto>(`/api/branches/${branchId}/floor-map`);
    }
  };
}

export function createSessionClient(api: PlatformApiClient) {
  return {
    startGuestSession(branchId: Guid, request: StartGuestSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, StartGuestSessionRequest>(`/api/branches/${branchId}/sessions/start`, request);
    },
    extendSession(sessionId: Guid, request: ExtendSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, ExtendSessionRequest>(`/api/sessions/${sessionId}/extend`, request);
    },
    transferSession(sessionId: Guid, request: TransferSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, TransferSessionRequest>(`/api/sessions/${sessionId}/transfer`, request);
    },
    endSession(sessionId: Guid, request: EndSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, EndSessionRequest>(`/api/sessions/${sessionId}/end`, request);
    }
  };
}

export function createPosClient(api: PlatformApiClient) {
  return {
    getCatalog(branchId: Guid): Promise<PosProductDto[]> {
      return api.get<PosProductDto[]>(`/api/branches/${branchId}/pos/catalog`);
    },
    createSale(branchId: Guid, request: CreatePosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, CreatePosSaleRequest>(`/api/branches/${branchId}/pos/sales`, request);
    },
    paySaleManual(saleId: Guid, request: ManualPaymentRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, ManualPaymentRequest>(`/api/pos/sales/${saleId}/payments/manual`, request);
    },
    refundSale(saleId: Guid, request: RefundPosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, RefundPosSaleRequest>(`/api/pos/sales/${saleId}/refunds`, request);
    },
    voidSale(saleId: Guid, request: VoidPosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, VoidPosSaleRequest>(`/api/pos/sales/${saleId}/void`, request);
    },
    getSale(saleId: Guid): Promise<PosSaleDto> {
      return api.get<PosSaleDto>(`/api/pos/sales/${saleId}`);
    },
    getReceipt(receiptId: Guid): Promise<ReceiptDto> {
      return api.get<ReceiptDto>(`/api/receipts/${receiptId}`);
    }
  };
}

export function createPlayerClient(api: PlatformApiClient) {
  return {
    searchPlayers(branchId: Guid, query: string, limit: number): Promise<PlayerSearchResultDto[]> {
      return api.get<PlayerSearchResultDto[]>(`/api/branches/${branchId}/players`, { query, limit });
    },
    createPlayer(branchId: Guid, request: CreatePlayerAccountRequest): Promise<PlayerAccountDto> {
      return api.post<PlayerAccountDto, CreatePlayerAccountRequest>(`/api/branches/${branchId}/players`, request);
    },
    getWalletSummary(playerAccountId: Guid): Promise<WalletSummaryDto> {
      return api.get<WalletSummaryDto>(`/api/players/${playerAccountId}/wallet-summary`);
    },
    getPlayerPackages(playerAccountId: Guid): Promise<PlayerPackageDto[]> {
      return api.get<PlayerPackageDto[]>(`/api/players/${playerAccountId}/packages`);
    },
    purchasePackage(playerAccountId: Guid, request: PurchasePackageRequest): Promise<PlayerPackageDto> {
      return api.post<PlayerPackageDto, PurchasePackageRequest>(`/api/players/${playerAccountId}/packages/purchases`, request);
    },
    topUpWallet(playerAccountId: Guid, request: TopUpWalletRequest): Promise<WalletSummaryDto> {
      return api.post<WalletSummaryDto, TopUpWalletRequest>(`/api/players/${playerAccountId}/wallet/top-ups`, request);
    },
    payDebt(playerAccountId: Guid, request: PayDebtRequest): Promise<WalletSummaryDto> {
      return api.post<WalletSummaryDto, PayDebtRequest>(`/api/players/${playerAccountId}/debts/payments`, request);
    }
  };
}

export function createDashboardClient(api: PlatformApiClient) {
  return {
    getSummary(branchId: Guid, query?: DashboardSummaryQuery): Promise<OperatorDashboardSummaryDto> {
      return api.get<OperatorDashboardSummaryDto>(`/api/branches/${branchId}/dashboard/summary`, normalizeReportQuery(query));
    }
  };
}

export function createReservationClient(api: PlatformApiClient) {
  return {
    search(branchId: Guid, query?: ReservationSearchQuery): Promise<ReservationSearchResultDto> {
      return api.get<ReservationSearchResultDto>(`/api/branches/${branchId}/reservations`, normalizeReportQuery(query));
    },
    create(branchId: Guid, request: CreateReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, CreateReservationRequest>(`/api/branches/${branchId}/reservations`, request);
    },
    update(reservationId: Guid, request: UpdateReservationRequest): Promise<ReservationDto> {
      return api.patch<ReservationDto, UpdateReservationRequest>(`/api/reservations/${reservationId}`, request);
    },
    confirm(reservationId: Guid, request: ConfirmReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, ConfirmReservationRequest>(`/api/reservations/${reservationId}/confirm`, request);
    },
    seat(reservationId: Guid, request: SeatReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, SeatReservationRequest>(`/api/reservations/${reservationId}/seat`, request);
    },
    cancel(reservationId: Guid, request: CancelReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, CancelReservationRequest>(`/api/reservations/${reservationId}/cancel`, request);
    }
  };
}

export function createShiftClient(api: PlatformApiClient) {
  return {
    openShift(branchId: Guid, request: OpenShiftRequest): Promise<ShiftDto> {
      return api.post<ShiftDto, OpenShiftRequest>(`/api/branches/${branchId}/shifts/open`, request);
    },
    getCurrentShift(branchId: Guid): Promise<ShiftDto | null> {
      return api.getOptional<ShiftDto>(`/api/branches/${branchId}/shifts/current`);
    },
    recordCashMovement(shiftId: Guid, request: RecordCashMovementRequest): Promise<CashMovementDto> {
      return api.post<CashMovementDto, RecordCashMovementRequest>(`/api/shifts/${shiftId}/cash-movements`, request);
    },
    closeShift(shiftId: Guid, request: CloseShiftRequest): Promise<ShiftDto> {
      return api.post<ShiftDto, CloseShiftRequest>(`/api/shifts/${shiftId}/close`, request);
    },
    getShiftReport(branchId: Guid, query?: ReportQuery): Promise<ReportResultDto> {
      return api.get<ReportResultDto>(`/api/branches/${branchId}/reports/shifts`, normalizeReportQuery(query));
    },
    getSalesReport(branchId: Guid, query?: ReportQuery): Promise<ReportResultDto> {
      return api.get<ReportResultDto>(`/api/branches/${branchId}/reports/sales`, normalizeReportQuery(query));
    },
    getGameplayTimeReport(branchId: Guid, query?: ReportQuery): Promise<ReportResultDto> {
      return api.get<ReportResultDto>(`/api/branches/${branchId}/reports/gameplay-time`, normalizeReportQuery(query));
    },
    getCashOperationReport(branchId: Guid, query?: ReportQuery): Promise<ReportResultDto> {
      return api.get<ReportResultDto>(`/api/branches/${branchId}/reports/cash-operations`, normalizeReportQuery(query));
    },
    getOperatorActionReport(branchId: Guid, query?: ReportQuery): Promise<ReportResultDto> {
      return api.get<ReportResultDto>(`/api/branches/${branchId}/reports/operator-actions`, normalizeReportQuery(query));
    },
    exportShiftReportCsv(branchId: Guid, query?: ReportQuery): Promise<string> {
      return api.getText(`/api/branches/${branchId}/reports/shifts/export.csv`, normalizeReportQuery(query));
    },
    exportSalesReportCsv(branchId: Guid, query?: ReportQuery): Promise<string> {
      return api.getText(`/api/branches/${branchId}/reports/sales/export.csv`, normalizeReportQuery(query));
    },
    exportGameplayTimeReportCsv(branchId: Guid, query?: ReportQuery): Promise<string> {
      return api.getText(`/api/branches/${branchId}/reports/gameplay-time/export.csv`, normalizeReportQuery(query));
    },
    exportCashOperationReportCsv(branchId: Guid, query?: ReportQuery): Promise<string> {
      return api.getText(`/api/branches/${branchId}/reports/cash-operations/export.csv`, normalizeReportQuery(query));
    },
    exportOperatorActionReportCsv(branchId: Guid, query?: ReportQuery): Promise<string> {
      return api.getText(`/api/branches/${branchId}/reports/operator-actions/export.csv`, normalizeReportQuery(query));
    }
  };
}

export function createSettingsClient(api: PlatformApiClient) {
  return {
    getBranchProfile(branchId: Guid): Promise<BranchProfileDto> {
      return api.get<BranchProfileDto>(`/api/branches/${branchId}/profile`);
    },
    updateBranchProfile(branchId: Guid, request: UpdateBranchProfileRequest): Promise<BranchProfileDto> {
      return api.patch<BranchProfileDto, UpdateBranchProfileRequest>(`/api/branches/${branchId}/profile`, request);
    },
    getStaffUsers(branchId: Guid): Promise<StaffUserDto[]> {
      return api.get<StaffUserDto[]>(`/api/branches/${branchId}/staff`);
    },
    createStaffUser(branchId: Guid, request: CreateStaffUserRequest): Promise<StaffUserDto> {
      return api.post<StaffUserDto, CreateStaffUserRequest>(`/api/branches/${branchId}/staff`, request);
    },
    updateStaffUserRoles(branchId: Guid, staffUserId: Guid, request: UpdateStaffUserRolesRequest): Promise<StaffUserDto> {
      return api.patch<StaffUserDto, UpdateStaffUserRolesRequest>(`/api/branches/${branchId}/staff/${staffUserId}/roles`, request);
    },
    getLayoutZones(branchId: Guid): Promise<ZoneDto[]> {
      return api.get<ZoneDto[]>(`/api/branches/${branchId}/layout/zones`);
    },
    createZone(branchId: Guid, request: CreateZoneRequest): Promise<ZoneDto> {
      return api.post<ZoneDto, CreateZoneRequest>(`/api/branches/${branchId}/layout/zones`, request);
    },
    createSeat(branchId: Guid, request: CreateSeatRequest): Promise<SeatDto> {
      return api.post<SeatDto, CreateSeatRequest>(`/api/branches/${branchId}/layout/seats`, request);
    },
    createTariff(branchId: Guid, request: CreateTariffRequest): Promise<TariffDto> {
      return api.post<TariffDto, CreateTariffRequest>(`/api/branches/${branchId}/tariffs`, request);
    },
    createTariffVersion(branchId: Guid, tariffId: Guid, request: CreateTariffVersionRequest): Promise<TariffVersionDto> {
      return api.post<TariffVersionDto, CreateTariffVersionRequest>(`/api/branches/${branchId}/tariffs/${tariffId}/versions`, request);
    },
    getTariffOptions(branchId: Guid): Promise<TariffOptionDto[]> {
      return api.get<TariffOptionDto[]>(`/api/branches/${branchId}/tariffs/options`);
    },
    getPackageOptions(branchId: Guid): Promise<PackageOptionDto[]> {
      return api.get<PackageOptionDto[]>(`/api/branches/${branchId}/packages/options`);
    },
    createPackageDefinition(branchId: Guid, request: CreatePackageDefinitionRequest): Promise<PackageDefinitionDto> {
      return api.post<PackageDefinitionDto, CreatePackageDefinitionRequest>(`/api/branches/${branchId}/packages`, request);
    },
    updatePackageDefinition(branchId: Guid, packageDefinitionId: Guid, request: UpdatePackageDefinitionRequest): Promise<PackageDefinitionDto> {
      return api.patch<PackageDefinitionDto, UpdatePackageDefinitionRequest>(`/api/branches/${branchId}/packages/${packageDefinitionId}`, request);
    },
    createProductCategory(branchId: Guid, request: CreateProductCategoryRequest): Promise<PosProductCategoryDto> {
      return api.post<PosProductCategoryDto, CreateProductCategoryRequest>(`/api/branches/${branchId}/pos/categories`, request);
    },
    createProduct(branchId: Guid, request: CreateProductRequest): Promise<PosProductDto> {
      return api.post<PosProductDto, CreateProductRequest>(`/api/branches/${branchId}/pos/products`, request);
    },
    updateProduct(branchId: Guid, productId: Guid, request: UpdateProductRequest): Promise<PosProductDto> {
      return api.patch<PosProductDto, UpdateProductRequest>(`/api/branches/${branchId}/pos/products/${productId}`, request);
    },
    assignDeviceSeat(branchId: Guid, deviceId: Guid, request: AssignDeviceSeatRequest): Promise<DeviceSeatAssignmentDto> {
      return api.post<DeviceSeatAssignmentDto, AssignDeviceSeatRequest>(`/api/branches/${branchId}/devices/${deviceId}/seat-assignment`, request);
    }
  };
}

export function createInventoryClient(api: PlatformApiClient) {
  return {
    getStockMovements(branchId: Guid, query?: StockMovementSearchQuery): Promise<StockMovementDto[]> {
      return api.get<StockMovementDto[]>(`/api/branches/${branchId}/inventory/stock-movements`, normalizeReportQuery(query));
    },
    createStockMovement(branchId: Guid, request: CreateStockMovementRequest): Promise<StockMovementDto> {
      return api.post<StockMovementDto, CreateStockMovementRequest>(`/api/branches/${branchId}/inventory/stock-movements`, request);
    }
  };
}

export function createDeviceClient(api: PlatformApiClient) {
  return {
    createEnrollmentCode(branchId: Guid, organizationId: Guid, expiresInSeconds: number): Promise<DeviceEnrollmentCodeDto> {
      return api.post<DeviceEnrollmentCodeDto>(`/api/branches/${branchId}/device-enrollment-codes`, {
        organizationId,
        expiresInSeconds
      });
    },
    dispatchDeviceCommand(deviceId: Guid, request: DispatchDeviceCommandRequest): Promise<DeviceCommandDto> {
      return api.post<DeviceCommandDto, DispatchDeviceCommandRequest>(`/api/devices/${deviceId}/commands`, request);
    },
    getDeviceCommandStatus(deviceId: Guid, commandId: Guid): Promise<DeviceCommandStatusDto> {
      return api.get<DeviceCommandStatusDto>(`/api/devices/${deviceId}/commands/${commandId}/status`);
    },
    getDeviceDetail(deviceId: Guid): Promise<DeviceDetailDto> {
      return api.get<DeviceDetailDto>(`/api/devices/${deviceId}`);
    },
    rotateDeviceCredential(deviceId: Guid): Promise<RotateDeviceCredentialResponse> {
      return api.post<RotateDeviceCredentialResponse>(`/api/devices/${deviceId}/credentials/rotate`);
    },
    revokeDeviceCredential(deviceId: Guid, credentialId: Guid): Promise<RevokeDeviceCredentialResponse> {
      return api.post<RevokeDeviceCredentialResponse>(`/api/devices/${deviceId}/credentials/${credentialId}/revoke`);
    }
  };
}

export function createDiagnosticsClient(api: PlatformApiClient) {
  return {
    getDiagnostics(branchId: Guid): Promise<BranchDiagnosticsDto> {
      return api.get<BranchDiagnosticsDto>(`/api/branches/${branchId}/diagnostics`);
    }
  };
}

export function createUpdateClient(api: PlatformApiClient) {
  return {
    getRolloutStatuses(branchId: Guid): Promise<UpdateRolloutStatusDto[]> {
      return api.get<UpdateRolloutStatusDto[]>(`/api/branches/${branchId}/updates/rollouts`);
    },
    registerPackage(branchId: Guid, request: CreateUpdatePackageRequest): Promise<UpdatePackageDto> {
      return api.post<UpdatePackageDto, CreateUpdatePackageRequest>(`/api/branches/${branchId}/updates/packages`, request);
    },
    changePackageState(branchId: Guid, updatePackageId: Guid, request: UpdatePackageStateChangeRequest): Promise<UpdatePackageDto> {
      return api.post<UpdatePackageDto, UpdatePackageStateChangeRequest>(`/api/branches/${branchId}/updates/packages/${updatePackageId}/state`, request);
    },
    createRollout(branchId: Guid, request: CreateUpdateRolloutRequest): Promise<UpdateRolloutDto> {
      return api.post<UpdateRolloutDto, CreateUpdateRolloutRequest>(`/api/branches/${branchId}/updates/rollouts`, request);
    },
    changeRolloutState(branchId: Guid, updateRolloutId: Guid, request: UpdateRolloutStateChangeRequest): Promise<UpdateRolloutDto> {
      return api.post<UpdateRolloutDto, UpdateRolloutStateChangeRequest>(`/api/branches/${branchId}/updates/rollouts/${updateRolloutId}/state`, request);
    }
  };
}

export function createAuditClient(api: PlatformApiClient) {
  return {
    search(request: AuditSearchRequest): Promise<AuditSearchResultDto> {
      const { branchId, ...query } = request;
      return api.get<AuditSearchResultDto>(`/api/branches/${branchId}/audit`, normalizeReportQuery(query));
    }
  };
}

type NormalizedQuery = ReportQuery | ReservationSearchQuery | StockMovementSearchQuery | Omit<AuditSearchRequest, 'branchId'>;

function normalizeReportQuery(query?: NormalizedQuery): QueryParams | undefined {
  if (!query) {
    return undefined;
  }

  return {
    ...query,
    fromUtc: query.fromUtc instanceof Date ? query.fromUtc.toISOString() : query.fromUtc,
    toUtc: query.toUtc instanceof Date ? query.toUtc.toISOString() : query.toUtc
  };
}
