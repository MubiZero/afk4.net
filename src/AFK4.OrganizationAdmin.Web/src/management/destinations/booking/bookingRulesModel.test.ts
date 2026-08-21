import { describe, expect, it } from 'bun:test';
import {
  bookingRulesDefaults,
  bookingRulesToForm,
  buildBookingRulesRequest,
  isBookingRulesFormValid
} from './bookingRulesModel';
import type { BranchBookingSettingsDto } from '../../../api/clients/settings';

const organizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';

function dto(overrides: Partial<BranchBookingSettingsDto> = {}): BranchBookingSettingsDto {
  return {
    organizationId,
    branchId: 'b1',
    acceptanceMode: 'manual',
    respondWithinMinutes: 30,
    requirePrepaymentFromNewGuests: false,
    maxActiveReservationsForNewGuests: 3,
    regularAfterVisits: 5,
    holdSeatAfterStartMinutes: 0,
    keepPrepaymentOnNoShow: true,
    updatedAtUtc: '2026-08-19T10:00:00Z',
    ...overrides
  };
}

describe('bookingRulesToForm', () => {
  it('переносит настройки филиала в поля формы', () => {
    expect(bookingRulesToForm(dto())).toEqual({
      acceptanceMode: 'manual',
      respondWithinMinutes: '30',
      requirePrepaymentFromNewGuests: false,
      maxActiveReservationsForNewGuests: '3',
      regularAfterVisits: '5',
      holdSeatAfterStartMinutes: '0',
      keepPrepaymentOnNoShow: true
    });
  });

  it('значения по умолчанию совпадают с серверными BranchBookingSettingsDefaults', () => {
    // Ненастроенный филиал: сервер отдаёт auto / 15 минут / предоплата с новых / 1 бронь /
    // 3 визита / 20 минут держим место / предоплату при неявке не удерживаем.
    expect(bookingRulesDefaults).toEqual({
      acceptanceMode: 'auto',
      respondWithinMinutes: '15',
      requirePrepaymentFromNewGuests: true,
      maxActiveReservationsForNewGuests: '1',
      regularAfterVisits: '3',
      holdSeatAfterStartMinutes: '20',
      keepPrepaymentOnNoShow: false
    });
  });
});

describe('buildBookingRulesRequest', () => {
  it('собирает тело PUT из формы, превращая строки в числа', () => {
    expect(buildBookingRulesRequest(organizationId, bookingRulesToForm(dto()))).toEqual({
      organizationId,
      acceptanceMode: 'manual',
      respondWithinMinutes: 30,
      requirePrepaymentFromNewGuests: false,
      maxActiveReservationsForNewGuests: 3,
      regularAfterVisits: 5,
      holdSeatAfterStartMinutes: 0,
      keepPrepaymentOnNoShow: true
    });
  });

  it('не собирает тело, если число вне границ сервера', () => {
    const form = bookingRulesToForm(dto({ respondWithinMinutes: 30 }));
    expect(buildBookingRulesRequest(organizationId, { ...form, respondWithinMinutes: '4' })).toBeNull();
    expect(buildBookingRulesRequest(organizationId, { ...form, respondWithinMinutes: '1441' })).toBeNull();
    expect(buildBookingRulesRequest(organizationId, { ...form, maxActiveReservationsForNewGuests: '0' })).toBeNull();
    expect(buildBookingRulesRequest(organizationId, { ...form, maxActiveReservationsForNewGuests: '21' })).toBeNull();
    expect(buildBookingRulesRequest(organizationId, { ...form, regularAfterVisits: '101' })).toBeNull();
    expect(buildBookingRulesRequest(organizationId, { ...form, holdSeatAfterStartMinutes: '241' })).toBeNull();
  });

  it('пустое и дробное поле — не число, а не ноль', () => {
    const form = bookingRulesToForm(dto());
    expect(buildBookingRulesRequest(organizationId, { ...form, holdSeatAfterStartMinutes: '' })).toBeNull();
    expect(buildBookingRulesRequest(organizationId, { ...form, holdSeatAfterStartMinutes: '10.5' })).toBeNull();
  });

  it('нулевые границы законны: ноль визитов и ноль минут удержания места', () => {
    const form = bookingRulesToForm(dto());
    const request = buildBookingRulesRequest(organizationId, {
      ...form,
      regularAfterVisits: '0',
      holdSeatAfterStartMinutes: '0'
    });
    expect(request).toMatchObject({ regularAfterVisits: 0, holdSeatAfterStartMinutes: 0 });
  });

  it('неизвестный режим приёма не отправляется', () => {
    const form = bookingRulesToForm(dto());
    expect(buildBookingRulesRequest(organizationId, { ...form, acceptanceMode: 'sometimes' })).toBeNull();
  });
});

describe('isBookingRulesFormValid', () => {
  it('повторяет решение сборки тела', () => {
    const form = bookingRulesToForm(dto());
    expect(isBookingRulesFormValid(form)).toBe(true);
    expect(isBookingRulesFormValid({ ...form, respondWithinMinutes: '2' })).toBe(false);
  });
});
