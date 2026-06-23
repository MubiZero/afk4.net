import { describe, expect, it } from 'bun:test';
import { fixturePlayers, playerStatusLabel, projectPlayerClient, ledgerTypeLabel, projectLedgerEntry, projectPlayerPackage, buildClientSegments, buildClientOverview, buildClientContext, matchesSegment, type ClientSegmentId } from './playersModel';
import type { TFunc, PlayerClientItem } from '../operatorHelpers';
import type { LedgerEntryDto, PlayerPackageDto, SessionTimelineItemDto } from '../operatorApiClients';

// Стаб переводчика: возвращает ключ, игнорируя параметры — тесты проверяют только
// структурные поля проекции, не локализованный текст.
const t = ((key: string) => key) as unknown as TFunc;

describe('playerStatusLabel', () => {
  it('maps known status keys to localized keys and passes through unknown', () => {
    expect(playerStatusLabel('debt', t)).toBe('op.players.status.debt');
    expect(playerStatusLabel('inactive', t)).toBe('op.players.status.inactive');
    expect(playerStatusLabel('active', t)).toBe('op.players.status.active');
    expect(playerStatusLabel('mystery', t)).toBe('mystery');
  });
});

describe('fixturePlayers', () => {
  it('returns three offline-fixture clients with stable tones', () => {
    const players = fixturePlayers('TJS', t);
    expect(players).toHaveLength(3);
    expect(players.map((p) => p.tone)).toEqual(['active', 'active', 'debt']);
    expect(players.map((p) => p.name)).toEqual(['Madina S.', 'Amir K.', 'Olim K.']);
    expect(players.every((p) => p.source === 'fixture')).toBe(true);
  });
});

describe('projectPlayerClient', () => {
  it('derives status/tone from debt and active-state only (packages do not change status)', () => {
    const debtor = projectPlayerClient(
      { playerAccountId: 'p1', displayName: 'Olim', walletBalanceMinorUnits: 0, debtBalanceMinorUnits: 3500, activePackageCount: 0, isActive: true },
      t
    );
    expect(debtor.status).toBe('debt');
    expect(debtor.tone).toBe('debt');
    expect(debtor.debtMinorUnits).toBe(3500);
    expect(debtor.source).toBe('backend');

    // Активный клиент с пакетами, но без долга — обычный 'active' (пакеты не делают особый статус).
    const withPackages = projectPlayerClient(
      { playerAccountId: 'p2', displayName: 'Madina', walletBalanceMinorUnits: 46000, debtBalanceMinorUnits: 0, activePackageCount: 2, isActive: true },
      t
    );
    expect(withPackages.status).toBe('active');
    expect(withPackages.tone).toBe('active');
    expect(withPackages.balanceMinorUnits).toBe(46000);

    const inactive = projectPlayerClient(
      { playerAccountId: 'p3', displayName: 'Ghost', walletBalanceMinorUnits: 0, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: false },
      t
    );
    expect(inactive.status).toBe('inactive');
  });
});

// ─── Новые тесты S1 ──────────────────────────────────────────────────────────

const ledger = (over: Partial<LedgerEntryDto>): LedgerEntryDto => ({
  ledgerEntryId: 'le-x', organizationId: 'o', branchId: 'b', playerAccountId: 'p',
  sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 5000 }, quantitySeconds: 0,
  description: '', reason: '', reversesLedgerEntryId: null,
  createdByStaffUserId: 's', createdAtUtc: '2026-06-22T09:00:00Z', ...over
});

const pkg = (over: Partial<PlayerPackageDto>): PlayerPackageDto => ({
  playerPackageId: 'pp-x', packageDefinitionId: 'pd-x', playerAccountId: 'p',
  name: 'Ночной 5ч', purchasedPrice: { currencyCode: 'TJS', minorUnits: 25000 },
  includedSeconds: 18000, bonusSeconds: 1800,
  remainingIncludedSeconds: 9000, remainingBonusSeconds: 1800,
  purchasedAtUtc: '2026-06-21T09:00:00Z', expiresAtUtc: null, ...over
});

const client = (over: Partial<PlayerClientItem>): PlayerClientItem => ({
  playerAccountId: 'p', name: 'X', status: 'active', balanceMinorUnits: 0,
  debtMinorUnits: 0, last: '', tone: 'active', detail: '', phoneNumber: '',
  source: 'backend', ...over
});

describe('ledgerTypeLabel', () => {
  it('maps known entry types to the shared ledger.type.* keys, unknown → fallback key', () => {
    expect(ledgerTypeLabel('top_up', t)).toBe('ledger.type.top_up');
    expect(ledgerTypeLabel('gameplay_charge', t)).toBe('ledger.type.gameplay_charge');
    expect(ledgerTypeLabel('package_purchase', t)).toBe('ledger.type.package_purchase');
    expect(ledgerTypeLabel('bonus_grant', t)).toBe('ledger.type.bonus_grant');
    expect(ledgerTypeLabel('debt_payment', t)).toBe('ledger.type.debt_payment');
    expect(ledgerTypeLabel('refund', t)).toBe('ledger.type.refund');
    expect(ledgerTypeLabel('manual_correction', t)).toBe('ledger.type.manual_correction');
    expect(ledgerTypeLabel('wallet_payment', t)).toBe('ledger.type.wallet_payment');
    expect(ledgerTypeLabel('cashback', t)).toBe('ledger.type.cashback');
    expect(ledgerTypeLabel('reversal', t)).toBe('ledger.type.reversal');
    expect(ledgerTypeLabel('mystery_type', t)).toBe('op.players.ledger.type.fallback');
  });
});

describe('projectLedgerEntry', () => {
  it('projects credit/debit by amount sign and flags reversals', () => {
    const credit = projectLedgerEntry(ledger({ ledgerEntryId: 'le-1', entryType: 'top_up', amount: { currencyCode: 'TJS', minorUnits: 5000 }, description: 'Пополнение', reason: 'Касса' }), t);
    expect(credit.id).toBe('le-1');
    expect(credit.isCredit).toBe(true);
    expect(credit.isReversal).toBe(false);
    expect(credit.amountMinorUnits).toBe(5000);
    expect(credit.currencyCode).toBe('TJS');
    expect(credit.description).toBe('Пополнение');
    expect(credit.reason).toBe('Касса');
    expect(credit.typeLabel).toBe('ledger.type.top_up');

    const debit = projectLedgerEntry(ledger({ entryType: 'gameplay_charge', amount: { currencyCode: 'TJS', minorUnits: -1200 } }), t);
    expect(debit.isCredit).toBe(false);

    const reversal = projectLedgerEntry(ledger({ entryType: 'refund', reversesLedgerEntryId: 'le-1' }), t);
    expect(reversal.isReversal).toBe(true);
  });
});

describe('projectPlayerPackage', () => {
  it('converts remaining seconds to minutes and labels expiry / perpetual / expired', () => {
    const perpetual = projectPlayerPackage(pkg({ expiresAtUtc: null }), t, 'ru-RU');
    expect(perpetual.remainingIncludedMinutes).toBe(150); // 9000/60
    expect(perpetual.remainingBonusMinutes).toBe(30);     // 1800/60
    expect(perpetual.totalRemainingMinutes).toBe(180);
    expect(perpetual.expiryLabel).toBeNull();
    expect(perpetual.isExpired).toBe(false);

    const dated = projectPlayerPackage(pkg({ expiresAtUtc: '2099-01-01T00:00:00Z' }), t, 'ru-RU');
    expect(typeof dated.expiryLabel).toBe('string');
    expect(dated.isExpired).toBe(false);

    const expired = projectPlayerPackage(pkg({ expiresAtUtc: '2000-01-01T00:00:00Z' }), t, 'ru-RU');
    expect(expired.isExpired).toBe(true);
  });
});

describe('client segments (stable ids — survive locale change)', () => {
  it('buildClientSegments returns three stable-id segments with correct counts', () => {
    const clients = [
      client({ debtMinorUnits: 3500, status: 'debt' }),
      client({ status: 'inactive', tone: 'regular' }),
      client({ status: 'active' })
    ];
    const segments = buildClientSegments(clients, t);
    expect(segments.map((s) => s.id)).toEqual(['all', 'debt', 'inactive']);
    const byId = (id: ClientSegmentId) => segments.find((s) => s.id === id)!;
    expect(byId('all').count).toBe(3);
    expect(byId('debt').count).toBe(1);
    expect(byId('inactive').count).toBe(1);
    expect(byId('all').label).toBe('op.players.segments.all');
  });

  it('matchesSegment filters by real fields', () => {
    expect(matchesSegment(client({ status: 'active' }), 'all')).toBe(true);
    expect(matchesSegment(client({ debtMinorUnits: 100 }), 'debt')).toBe(true);
    expect(matchesSegment(client({ debtMinorUnits: 0 }), 'debt')).toBe(false);
    expect(matchesSegment(client({ status: 'inactive' }), 'inactive')).toBe(true);
    expect(matchesSegment(client({ status: 'active' }), 'inactive')).toBe(false);
  });
});

describe('buildClientOverview (сводка по базе для шапки)', () => {
  it('counts clients and sums positive deposits and debts', () => {
    const overview = buildClientOverview([
      client({ balanceMinorUnits: 45000, debtMinorUnits: 0 }),
      client({ balanceMinorUnits: 12000, debtMinorUnits: 0 }),
      client({ balanceMinorUnits: 0, debtMinorUnits: 3500 })
    ]);
    expect(overview.count).toBe(3);
    expect(overview.depositMinorUnits).toBe(57000);
    expect(overview.debtMinorUnits).toBe(3500);
  });

  it('ignores negative balances in the deposit sum and returns zeros for an empty base', () => {
    expect(buildClientOverview([client({ balanceMinorUnits: -500, debtMinorUnits: 0 })]).depositMinorUnits).toBe(0);
    expect(buildClientOverview([])).toEqual({ count: 0, depositMinorUnits: 0, debtMinorUnits: 0 });
  });
});

const session = (over: Partial<SessionTimelineItemDto>): SessionTimelineItemDto => ({
  sessionId: 's', seatId: 'seat', seatName: 'PC-03', zoneId: 'z', zoneName: 'Зал A',
  state: 'active', playerAccountId: 'p1', playerDisplayName: 'X', tariffName: null,
  startedAtUtc: '2026-06-23T10:00:00Z', endsAtUtc: '2026-06-23T11:00:00Z', endedAtUtc: null, ...over
});

describe('buildClientContext (играет сейчас + ближайшая бронь)', () => {
  it('picks the active blocking session for the player and projects seat + until', () => {
    const ctx = buildClientContext(
      [session({ playerAccountId: 'p1', seatName: 'PC-03' }), session({ playerAccountId: 'other', seatName: 'PC-09' })],
      [],
      'p1'
    );
    expect(ctx.session?.seatName).toBe('PC-03');
    expect(ctx.session?.untilLabel).not.toBeNull();
    expect(ctx.nextBooking).toBeNull();
  });

  it('treats an open tab (no scheduled end) as untilLabel null', () => {
    const ctx = buildClientContext([session({ playerAccountId: 'p1', endsAtUtc: null })], [], 'p1');
    expect(ctx.session?.untilLabel).toBeNull();
  });

  it('ignores ended, non-blocking and other-player sessions', () => {
    const ctx = buildClientContext(
      [
        session({ playerAccountId: 'p1', endedAtUtc: '2026-06-23T10:30:00Z' }),
        session({ playerAccountId: 'p1', state: 'requested' }),
        session({ playerAccountId: 'p2' })
      ],
      [],
      'p1'
    );
    expect(ctx.session).toBeNull();
  });

  it('selects the first pending/confirmed booking and skips cancelled', () => {
    const ctx = buildClientContext(
      [],
      [
        { reservationId: 'r0', state: 'cancelled', startsAtUtc: '2026-06-23T17:00:00Z', seatName: 'PC-01' },
        { reservationId: 'r1', state: 'confirmed', startsAtUtc: '2026-06-23T18:00:00Z', seatName: 'PC-02' }
      ],
      'p1'
    );
    expect(ctx.nextBooking?.seatName).toBe('PC-02');
    expect(ctx.nextBooking?.timeLabel).not.toBe('');
  });
});
