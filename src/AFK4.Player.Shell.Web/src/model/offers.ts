import type {
  PlayerDurationOfferDto,
  PlayerPackageOfferDto,
  PlayerSelfStartRequest,
  PlayerTariffOfferDto
} from '@afk4/contracts';
import type { Locale, MessageKey } from '@afk4/i18n';
import { PlayerApiError } from '../api/playerApi';
import { formatDateParts } from '@afk4/formatting';

/** Что выбрал игрок: вариант тарифа с готовой суммой или минуты из своего пакета. */
export type OfferChoice =
  | { kind: 'tariff'; tariff: PlayerTariffOfferDto; option: PlayerDurationOfferDto }
  | { kind: 'package'; offer: PlayerPackageOfferDto; minutes: number };

/** Сколько брать из пакета: те же часы, что у тарифов, пока хватает остатка, и «всё оставшееся». */
const PACKAGE_STEPS = [60, 120, 180, 300];

export function packageMinuteOptions(remainingMinutes: number): number[] {
  const steps = PACKAGE_STEPS.filter((minutes) => minutes <= remainingMinutes);
  return remainingMinutes > 0 && !steps.includes(remainingMinutes) ? [...steps, remainingMinutes] : steps;
}

export const INTL_LOCALES: Record<Locale, string> = { ru: 'ru-RU', tg: 'tg-TJ', en: 'en-GB' };

/** «до 21:40» — по часам клуба, а не по поясу, выставленному на ПК. Пояс не пришёл — время ПК. */
export function clubTime(isoUtc: string, timeZone: string | undefined, locale: Locale): string {
  try {
    return formatDateParts(isoUtc, INTL_LOCALES[locale], { hour: '2-digit', minute: '2-digit', timeZone });
  } catch {
    // Неизвестный пояс — время ПК лучше, чем ничего.
    return formatDateParts(isoUtc, INTL_LOCALES[locale], { hour: '2-digit', minute: '2-digit' });
  }
}

export function durationKey(minutes: number): { key: MessageKey; values: Record<string, number> } {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) return { key: 'playerShell.duration.minutes', values: { minutes: rest } };
  if (rest === 0) return { key: 'playerShell.duration.hours', values: { hours } };
  return { key: 'playerShell.duration.hoursMinutes', values: { hours, minutes: rest } };
}

/**
 * Тело старта. Код посадки пуст: токен оболочки привязан к ПК, а код вход по QR уже погасил. По
 * пакету тариф не передаётся — у пакета своя, уже заплаченная цена.
 */
export function startRequest(choice: OfferChoice, idempotencyKey: string): PlayerSelfStartRequest {
  return choice.kind === 'tariff'
    ? {
        seatingCode: '',
        tariffRuleVersionId: choice.tariff.tariffRuleVersionId,
        durationMinutes: choice.option.minutes,
        idempotencyKey,
        playerPackageId: null
      }
    : {
        seatingCode: '',
        tariffRuleVersionId: '',
        durationMinutes: choice.minutes,
        idempotencyKey,
        playerPackageId: choice.offer.playerPackageId
      };
}

/** Отказ старта — словами, по коду сервера. После части отказов цены надо перечитать. */
export function startErrorKey(reason: unknown): { key: MessageKey; reload: boolean } {
  if (!(reason instanceof PlayerApiError)) {
    // fetch падает TypeError, когда до сервера не дошли вовсе.
    return { key: 'playerShell.chooseTime.error.offline', reload: false };
  }

  switch (reason.code) {
    case 'insufficient_balance':
      return { key: 'playerShell.chooseTime.error.insufficient', reload: true };
    case 'tariff_outside_its_hours':
    case 'invalid_tariff':
      return { key: 'playerShell.chooseTime.error.tariffClosed', reload: true };
    case 'device_in_maintenance':
      return { key: 'playerShell.chooseTime.error.maintenance', reload: false };
    case 'device_outside_plan':
      return { key: 'playerShell.chooseTime.error.outsidePlan', reload: false };
    case 'seat_occupied':
      return { key: 'playerShell.chooseTime.error.taken', reload: false };
    default:
      return { key: 'playerShell.chooseTime.error.generic', reload: true };
  }
}
