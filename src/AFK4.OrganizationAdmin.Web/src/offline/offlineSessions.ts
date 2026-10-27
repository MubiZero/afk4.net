import type { TariffOptionDto } from '@afk4/contracts';

/** Номер сессии у записей кассы, которые не о сессии: продажа и смена. */
export const NO_SESSION = '00000000-0000-0000-0000-000000000000';

/** Товар, проданный без связи: имя и цена — из кэша, по ним взяли деньги. */
export interface OfflineSaleLine {
  productId: string;
  name: string;
  quantity: number;
  unitPriceMinorUnits: number;
}

/**
 * Запись журнала кассы без интернета. Поля сервера (OfflineJournalRecordDto) плюс то, что нужно Панели,
 * пока связи нет: адрес ПК, номер аренды и её конец.
 */
export interface OfflineJournalRecord {
  id: string;
  kind: 'session_started' | 'session_extended' | 'session_ended' | 'sale' | 'shift_closed' | 'shift_opened';
  sessionId: string;
  occurredAtUtc: string;
  shiftId: string;
  seatId?: string;
  deviceId?: string;
  deviceAddress?: string;
  tariffRuleVersionId?: string;
  tariffName?: string;
  minutes?: number;
  chargeMinorUnits?: number;
  currencyCode?: string;
  sequence?: number;
  leaseExpiresAtUtc?: string;
  lines?: OfflineSaleLine[];
  /** Наличные в ящике: посчитанные при закрытии смены или размен при открытии. */
  cashMinorUnits?: number;
  note?: string;
}

/** Гость, посаженный без связи: то, что Панель знает о нём по журналу. */
export interface OfflineSession {
  sessionId: string;
  seatId: string;
  deviceId: string;
  deviceAddress: string;
  tariffRuleVersionId: string;
  tariffName: string;
  pricePerMinuteMinorUnits: number;
  roundingIncrementMinutes: number;
  currencyCode: string;
  startedAtUtc: string;
  endsAtUtc: string;
  sequence: number;
  paidMinorUnits: number;
  ended: boolean;
}

/**
 * Офлайн-сессии по журналу. Журнал — единственный источник: перезапуск Панели без связи не теряет
 * ни посаженных гостей, ни взятых денег.
 */
export function foldOfflineSessions(records: OfflineJournalRecord[], tariffs: TariffOptionDto[]): Map<string, OfflineSession> {
  const bySession = new Map<string, OfflineSession>();
  for (const record of records) {
    const current = bySession.get(record.sessionId);
    if (record.kind === 'session_started' && record.seatId && record.deviceId && record.deviceAddress) {
      const tariff = tariffs.find((option) => option.tariffRuleVersionId === record.tariffRuleVersionId);
      bySession.set(record.sessionId, {
        sessionId: record.sessionId,
        seatId: record.seatId,
        deviceId: record.deviceId,
        deviceAddress: record.deviceAddress,
        tariffRuleVersionId: record.tariffRuleVersionId ?? '',
        tariffName: record.tariffName ?? tariff?.name ?? '',
        pricePerMinuteMinorUnits: tariff?.pricePerMinuteMinorUnits ?? 0,
        roundingIncrementMinutes: tariff?.roundingIncrementMinutes ?? 1,
        currencyCode: record.currencyCode ?? 'TJS',
        startedAtUtc: record.occurredAtUtc,
        endsAtUtc: record.leaseExpiresAtUtc ?? record.occurredAtUtc,
        sequence: record.sequence ?? 1,
        paidMinorUnits: record.chargeMinorUnits ?? 0,
        ended: false
      });
    } else if (current && record.kind === 'session_extended') {
      current.endsAtUtc = record.leaseExpiresAtUtc ?? current.endsAtUtc;
      current.sequence = record.sequence ?? current.sequence;
      current.paidMinorUnits += record.chargeMinorUnits ?? 0;
    } else if (current && record.kind === 'session_ended') {
      current.sequence = record.sequence ?? current.sequence;
      current.ended = true;
    }
  }

  return bySession;
}

/**
 * Открытая смена без связи: та, что была открыта при обрыве, пока журнал её не закрыл, — или открытая
 * по журналу следующая. В неё ложатся деньги новых записей.
 */
export function currentOfflineShift(
  cached: { shiftId: string | null; shiftOpenedByStaffUserId?: string | null },
  records: OfflineJournalRecord[],
  staffUserId: string | null
): { shiftId: string | null; openedByMe: boolean } {
  let shiftId = cached.shiftId;
  let openedByMe = shiftId !== null && cached.shiftOpenedByStaffUserId === staffUserId;
  for (const record of records) {
    if (record.kind === 'shift_closed' && record.shiftId === shiftId) {
      shiftId = null;
      openedByMe = false;
    } else if (record.kind === 'shift_opened') {
      shiftId = record.shiftId;
      openedByMe = true;
    }
  }
  return { shiftId, openedByMe };
}

/** Идущие офлайн-сессии по месту: закончили кнопкой или вышло время — места они больше не держат. */
export function liveOfflineSessionsBySeat(sessions: Map<string, OfflineSession>, nowMs: number): Map<string, OfflineSession> {
  const bySeat = new Map<string, OfflineSession>();
  for (const session of sessions.values()) {
    if (!session.ended && Date.parse(session.endsAtUtc) > nowMs) bySeat.set(session.seatId, session);
  }
  return bySeat;
}

/**
 * Цена продления по правилам сервера (TariffBilling.ComputeForExtension): минимум тарифа уже оплачен на
 * старте, остаётся шаг округления вверх.
 */
export function extensionPrice(session: Pick<OfflineSession, 'pricePerMinuteMinorUnits' | 'roundingIncrementMinutes'>, minutes: number): number {
  if (!(minutes > 0)) return 0;
  const step = session.roundingIncrementMinutes;
  const billable = step > 1 ? Math.ceil(minutes / step) * step : minutes;
  return billable * session.pricePerMinuteMinorUnits;
}

/**
 * Действует ли тариф в этот момент — то же правило, что TariffSchedule.AppliesAt на сервере. Время —
 * часы этого ПК администратора: он стоит в самом клубе, а пояс филиала без связи не узнать.
 * Неделя с понедельника; окно через полночь принадлежит дню, в который началось.
 */
export function tariffAppliesAt(
  tariff: Pick<TariffOptionDto, 'appliesOnDaysMask' | 'appliesFromMinuteOfDay' | 'appliesToMinuteOfDay'>,
  at: Date
): boolean {
  const day = (at.getDay() + 6) % 7;
  const previousDay = (day + 6) % 7;
  const mask = tariff.appliesOnDaysMask ?? 0;
  const matches = (bit: number) => mask === 0 || (mask & (1 << bit)) !== 0;
  const minute = at.getHours() * 60 + at.getMinutes();
  const from = tariff.appliesFromMinuteOfDay;
  const to = tariff.appliesToMinuteOfDay;
  if (from == null || to == null || from === to) return matches(day);
  if (from < to) return matches(day) && minute >= from && minute < to;
  return (minute >= from && matches(day)) || (minute < to && matches(previousDay));
}
