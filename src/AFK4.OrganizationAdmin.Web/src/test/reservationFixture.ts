import type { ReservationDto } from '@afk4/contracts';

/**
 * Бронь целиком, с полями, которых тест не касается. Сервер отдаёт запись целиком, и мапперы
 * читают её как контракт, а не как мешок ключей; перечислять два десятка полей в каждом примере
 * значит прятать за ними то, ради чего пример написан.
 */
export function aReservation(overrides: Partial<ReservationDto> = {}): ReservationDto {
  const startsAtUtc = overrides.startsAtUtc ?? '2026-05-21T10:00:00Z';
  const durationMinutes = overrides.durationMinutes ?? 60;
  return {
    reservationId: 'r-1',
    organizationId: 'org-1',
    branchId: 'branch-1',
    playerAccountId: null,
    seatId: null,
    seatName: null,
    zoneName: null,
    customerName: 'Гость',
    phoneNumber: null,
    startsAtUtc,
    endsAtUtc: new Date(new Date(startsAtUtc).getTime() + durationMinutes * 60_000).toISOString(),
    durationMinutes,
    state: 'pending',
    source: 'operator',
    note: '',
    createdAtUtc: startsAtUtc,
    updatedAtUtc: startsAtUtc,
    cancelledAtUtc: null,
    cancelReason: '',
    reservationGroupId: null,
    version: 1,
    ...overrides
  };
}
