// Просьба прислать код. Клуб здесь не называется: человек входит номером, а не карточкой,
// заведённой в конкретном клубе.
export interface RegistrationStartRequest { phoneNumber: string; }

// Ответ одинаков для знакомого и незнакомого номера — по нему нельзя понять, есть ли такой
// человек в сети, и полей для этого здесь нет намеренно.
export interface RegistrationStartedResponse { expiresInSeconds: number; resendAfterSeconds: number; }

export interface RegistrationConfirmRequest { phoneNumber: string; code: string; }

// Сессия человека, а не клубного счёта: клуба может не быть вовсе — так выглядит тот, кто
// зарегистрировался дома и ещё никуда не заходил.
export interface PlatformPersonSessionResponse {
  playerAccountId: string | null;
  organizationId: string | null;
  displayName: string;
  phoneVerified: boolean;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  platformPersonId: string;
  preferredLocale: string | null;
  // Спрошены ли имя и язык. Решает сервер, а не клиент.
  profileCompleted: boolean;
}

export interface MePersonDto {
  platformPersonId: string;
  phoneNumber: string;
  displayName: string;
  preferredLocale: string | null;
  phoneVerified: boolean;
  // Сам PIN сервер не отдаёт никогда — только признак, задан он или ещё нет.
  pinSet: boolean;
  networkBanned: boolean;
}

// Один клуб глазами игрока. Общей суммы по клубам нет и не будет: у каждого клуба своя касса,
// и сложенное число нельзя потратить ни в одном из них.
export interface MyClubDto {
  organizationId: string;
  organizationName: string;
  playerAccountId: string;
  homeBranchId: string;
  currencyCode: string;
  walletBalanceMinorUnits: number;
  heldMinorUnits: number;
  debtMinorUnits: number;
  visitCount: number;
}

export interface MeDto { person: MePersonDto; clubs: MyClubDto[]; }

export interface UpdateMyProfileRequest { displayName: string; preferredLocale: string | null; }

export interface SetMyPinRequest { pin: string; }

export interface MoneyDto { currencyCode: string; minorUnits: number; }

// Keys returned by GET /api/me/features — only the ones currently enabled for the org.
export type PlayerFeatureKey = 'online_booking' | 'loyalty' | 'online_topup' | 'player_shop';

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

// Три числа кошелька. `heldBalance` уже вычтено из `walletBalance` — это ответ на вопрос
// «а куда делись мои деньги», а не четвёртое место, где они лежат.
export interface PlayerDashboardDto {
  walletBalance: MoneyDto;
  heldBalance: MoneyDto;
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
  // Докуда клуб обещал ответить на заявку. У подтверждённой брони его нет: отвечать больше не на что.
  respondByUtc?: string | null;
}

// Правила брони этого филиала для этого игрока: предоплата нужна именно ему, потолок заявок
// именно его. Ни одного поля про других игроков здесь нет.
export interface PlayerBookingRulesDto {
  branchId: string;
  acceptanceMode: 'auto' | 'manual' | 'off';
  respondWithinMinutes: number;
  prepaymentRequired: boolean;
  activeReservations: number;
  // Пусто — значит потолка нет: игрок в этом филиале уже свой.
  maxActiveReservations: number | null;
  holdSeatAfterStartMinutes: number;
}
