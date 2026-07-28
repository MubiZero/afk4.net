export interface PlayerSignInRequest { organizationId: string; phoneNumber: string; password: string; }

export interface PlayerSignInResponse {
  playerAccountId: string;
  organizationId: string;
  displayName: string;
  phoneVerified: boolean;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

export interface MoneyDto { currencyCode: string; minorUnits: number; }

export interface ActiveSessionDto {
  sessionId: string;
  seatId: string;
  seatName: string;
  startedAtUtc: string;
  durationMode: 'open' | 'fixed';
  remainingSeconds: number | null;
  accruedCostMinorUnits: number | null;
  currencyCode: string;
}

export interface PlayerDashboardDto {
  walletBalance: MoneyDto;
  debtBalance: MoneyDto;
  activeSession: ActiveSessionDto | null;
}

export interface OrganizationBrandingDto {
  organizationId: string;
  name: string;
  logoUrl: string | null;
  accentColor: string | null;
}

export interface CursorPage<T> {
  items: T[];
  nextCursor: string | null;
}

export interface PlayerVisitDto {
  sessionId: string;
  seatId: string;
  seatName: string;
  startedAtUtc: string;
  endedAtUtc: string | null;
  timeChargeMinorUnits: number;
  posTotalMinorUnits: number;
  grandTotalMinorUnits: number;
  currencyCode: string;
  hasReceipt: boolean;
}

export interface PlayerPurchaseLineDto {
  productName: string;
  quantity: number;
  unitPriceMinorUnits: number;
  lineTotalMinorUnits: number;
}

export interface PlayerVisitReceiptDto {
  receiptNumber: string;
  createdAtUtc: string;
  sessionId: string;
  seatName: string;
  startedAtUtc: string;
  endedAtUtc: string | null;
  timeChargeMinorUnits: number;
  posLines: PlayerPurchaseLineDto[];
  posTotalMinorUnits: number;
  grandTotalMinorUnits: number;
  currencyCode: string;
}

export interface PlayerPurchaseDto {
  posSaleId: string;
  createdAtUtc: string;
  totalMinorUnits: number;
  currencyCode: string;
  lines: PlayerPurchaseLineDto[];
}

export interface PlayerProfileDto {
  playerAccountId: string;
  displayName: string;
  phoneNumber: string | null;
  phoneVerified: boolean;
  preferredLocale: string | null;
  marketingOptIn: boolean;
}

export interface UpdatePlayerProfileRequest {
  preferredLocale?: string | null;
  marketingOptIn?: boolean | null;
}

export interface PlayerTopUpIntentRequest {
  amountMinorUnits: number;
  currencyCode?: string | null;
}

export interface PlayerTopUpIntentDto {
  paymentIntentId: string;
  amountMinorUnits: number;
  currencyCode: string;
  state: string;
  purpose: string;
  method: string;
  createdAtUtc: string;
  fulfilledAtUtc: string | null;
  isExpired: boolean;
}

export interface CreatePlayerReservationRequest {
  seatId?: string | null;
  startsAtUtc: string;
  endsAtUtc: string;
  note?: string | null;
}

export interface PlayerReservationDto {
  reservationId: string;
  seatId: string | null;
  seatName: string | null;
  startsAtUtc: string;
  endsAtUtc: string;
  state: string;
  note: string | null;
}
