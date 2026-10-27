import { describe, expect, it } from 'bun:test';
import type { TariffOptionDto } from '@afk4/contracts';
import { extensionPrice, foldOfflineSessions, liveOfflineSessionsBySeat, tariffAppliesAt, type OfflineJournalRecord } from './offlineSessions';

const tariff = {
  tariffRuleVersionId: 'tariff-v1',
  name: 'Дневной',
  pricePerMinuteMinorUnits: 200,
  minimumBillableMinutes: 60,
  roundingIncrementMinutes: 15,
  appliesOnDaysMask: 0,
  appliesFromMinuteOfDay: null,
  appliesToMinuteOfDay: null
} as unknown as TariffOptionDto;

function record(partial: Partial<OfflineJournalRecord> & Pick<OfflineJournalRecord, 'id' | 'kind'>): OfflineJournalRecord {
  return { sessionId: 's1', occurredAtUtc: '2026-10-27T10:00:00Z', shiftId: 'shift', sequence: 1, leaseExpiresAtUtc: '2026-10-27T11:00:00Z', ...partial };
}

describe('журнал кассы без связи', () => {
  it('складывает посадку, продление и конец в одну сессию', () => {
    const sessions = foldOfflineSessions([
      record({ id: 'a', kind: 'session_started', seatId: 'seat', deviceId: 'pc', deviceAddress: '192.168.1.5', tariffRuleVersionId: 'tariff-v1', chargeMinorUnits: 12000 }),
      record({ id: 'b', kind: 'session_extended', sequence: 2, leaseExpiresAtUtc: '2026-10-27T11:30:00Z', chargeMinorUnits: 6000 })
    ], [tariff]);

    const session = sessions.get('s1')!;
    expect(session.endsAtUtc).toBe('2026-10-27T11:30:00Z');
    expect(session.paidMinorUnits).toBe(18000);
    expect(session.sequence).toBe(2);
    expect(liveOfflineSessionsBySeat(sessions, Date.parse('2026-10-27T11:00:00Z')).get('seat')?.sessionId).toBe('s1');
    // Время вышло — место свободно.
    expect(liveOfflineSessionsBySeat(sessions, Date.parse('2026-10-27T11:31:00Z')).size).toBe(0);

    const ended = foldOfflineSessions([
      record({ id: 'a', kind: 'session_started', seatId: 'seat', deviceId: 'pc', deviceAddress: '192.168.1.5', tariffRuleVersionId: 'tariff-v1' }),
      record({ id: 'c', kind: 'session_ended', sequence: 2 })
    ], [tariff]);
    expect(liveOfflineSessionsBySeat(ended, Date.parse('2026-10-27T10:30:00Z')).size).toBe(0);
  });

  it('продление: минимум уже оплачен на старте, остаётся шаг округления — как на сервере', () => {
    expect(extensionPrice(tariff, 15)).toBe(3000);
    expect(extensionPrice(tariff, 20)).toBe(6000);
    expect(extensionPrice(tariff, 0)).toBe(0);
  });

  it('расписание тарифа — то же правило, что у сервера: неделя с понедельника, ночь принадлежит своему дню', () => {
    // Ночной: только пятница (бит 4), 22:00–06:00.
    const night = { appliesOnDaysMask: 1 << 4, appliesFromMinuteOfDay: 22 * 60, appliesToMinuteOfDay: 6 * 60 };
    expect(tariffAppliesAt(night, new Date(2026, 9, 30, 23, 0))).toBe(true); // пятница 23:00
    expect(tariffAppliesAt(night, new Date(2026, 9, 31, 1, 0))).toBe(true); // суббота 01:00 — ночь пятницы
    expect(tariffAppliesAt(night, new Date(2026, 9, 31, 23, 0))).toBe(false); // суббота 23:00
    expect(tariffAppliesAt({ appliesOnDaysMask: 0, appliesFromMinuteOfDay: null, appliesToMinuteOfDay: null }, new Date())).toBe(true);
  });
});
