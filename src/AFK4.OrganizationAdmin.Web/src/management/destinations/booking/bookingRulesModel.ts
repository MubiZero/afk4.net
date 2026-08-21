import type { BranchBookingSettingsDto, UpdateBranchBookingSettingsRequest } from '../../../api/clients/settings';

export const bookingAcceptanceModes = ['off', 'manual', 'auto'] as const;
export type BookingAcceptanceMode = (typeof bookingAcceptanceModes)[number];

// Границы те же, что проверяет сервер (BranchBookingSettingsDefaults.Validate). Держим их здесь,
// чтобы оператор упирался в понятную подсказку у поля, а не в 400 после нажатия «Сохранить»;
// последнее слово всё равно за сервером.
export const bookingRulesLimits = {
  respondWithinMinutes: { min: 5, max: 24 * 60 },
  maxActiveReservationsForNewGuests: { min: 1, max: 20 },
  regularAfterVisits: { min: 0, max: 100 },
  holdSeatAfterStartMinutes: { min: 0, max: 240 }
} as const;

// Числа живут в форме строкой, а не числом: стёртое поле должно оставаться пустым, а не
// превращаться в ноль под руками у оператора (тот же приём, что у координат в профиле клуба).
export interface BookingRulesForm {
  acceptanceMode: string;
  respondWithinMinutes: string;
  requirePrepaymentFromNewGuests: boolean;
  maxActiveReservationsForNewGuests: string;
  regularAfterVisits: string;
  holdSeatAfterStartMinutes: string;
  keepPrepaymentOnNoShow: boolean;
}

// Значения ненастроенного филиала — копия серверных BranchBookingSettingsDefaults. Нужны только
// на время загрузки и в режиме без бэкенда: настоящий ответ сервера всегда приходит целиком.
export const bookingRulesDefaults: BookingRulesForm = {
  acceptanceMode: 'auto',
  respondWithinMinutes: '15',
  requirePrepaymentFromNewGuests: true,
  maxActiveReservationsForNewGuests: '1',
  regularAfterVisits: '3',
  holdSeatAfterStartMinutes: '20',
  keepPrepaymentOnNoShow: false
};

export function bookingRulesToForm(settings: BranchBookingSettingsDto): BookingRulesForm {
  return {
    acceptanceMode: settings.acceptanceMode,
    respondWithinMinutes: String(settings.respondWithinMinutes),
    requirePrepaymentFromNewGuests: settings.requirePrepaymentFromNewGuests,
    maxActiveReservationsForNewGuests: String(settings.maxActiveReservationsForNewGuests),
    regularAfterVisits: String(settings.regularAfterVisits),
    holdSeatAfterStartMinutes: String(settings.holdSeatAfterStartMinutes),
    keepPrepaymentOnNoShow: settings.keepPrepaymentOnNoShow
  };
}

function readWholeNumber(raw: string, bounds: { min: number; max: number }): number | null {
  const trimmed = raw.trim();
  if (!/^\d+$/.test(trimmed)) return null;
  const value = Number(trimmed);
  return value >= bounds.min && value <= bounds.max ? value : null;
}

/** Тело PUT, или null — если хоть одно поле вне границ (тогда сохранять нечего). */
export function buildBookingRulesRequest(
  organizationId: string,
  form: BookingRulesForm
): UpdateBranchBookingSettingsRequest | null {
  if (!(bookingAcceptanceModes as readonly string[]).includes(form.acceptanceMode)) return null;

  const respondWithinMinutes = readWholeNumber(form.respondWithinMinutes, bookingRulesLimits.respondWithinMinutes);
  const maxActive = readWholeNumber(form.maxActiveReservationsForNewGuests, bookingRulesLimits.maxActiveReservationsForNewGuests);
  const regularAfterVisits = readWholeNumber(form.regularAfterVisits, bookingRulesLimits.regularAfterVisits);
  const holdSeat = readWholeNumber(form.holdSeatAfterStartMinutes, bookingRulesLimits.holdSeatAfterStartMinutes);
  if (respondWithinMinutes === null || maxActive === null || regularAfterVisits === null || holdSeat === null) {
    return null;
  }

  return {
    organizationId,
    acceptanceMode: form.acceptanceMode,
    respondWithinMinutes,
    requirePrepaymentFromNewGuests: form.requirePrepaymentFromNewGuests,
    maxActiveReservationsForNewGuests: maxActive,
    regularAfterVisits,
    holdSeatAfterStartMinutes: holdSeat,
    keepPrepaymentOnNoShow: form.keepPrepaymentOnNoShow
  };
}

export function isBookingRulesFormValid(form: BookingRulesForm): boolean {
  return buildBookingRulesRequest('00000000-0000-0000-0000-000000000000', form) !== null;
}
