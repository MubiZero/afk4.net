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

export interface TenantBrandingDto {
  organizationId: string;
  name: string;
  logoUrl: string | null;
  accentColor: string | null;
}
