import { describe, expect, it } from 'bun:test';
import { fixturePlayers, playerStatusLabel, projectPlayerClient, ledgerTypeLabel, projectLedgerEntry, projectPlayerPackage, buildClientSegments, matchesSegment, type ClientSegmentId } from './playersModel';
import type { TFunc, PlayerClientItem } from '../operatorHelpers';
import type { LedgerEntryDto, PlayerPackageDto } from '../operatorApiClients';

// Стаб переводчика: возвращает ключ, игнорируя параметры — тесты проверяют только
// структурные поля проекции, не локализованный текст.
const t = ((key: string) => key) as unknown as TFunc;

describe('playerStatusLabel', () => {
  it('maps known status keys to localized keys and passes through unknown', () => {
    expect(playerStatusLabel('vip', t)).toBe('op.players.status.vip');
    expect(playerStatusLabel('debt', t)).toBe('op.players.status.debt');
    expect(playerStatusLabel('inactive', t)).toBe('op.players.status.inactive');
    expect(playerStatusLabel('active', t)).toBe('op.players.status.active');
    expect(playerStatusLabel('package', t)).toBe('op.players.status.package');
    expect(playerStatusLabel('mystery', t)).toBe('mystery');
  });
});

describe('fixturePlayers', () => {
  it('returns three offline-fixture clients with stable tones', () => {
    const players = fixturePlayers('TJS', t);
    expect(players).toHaveLength(3);
    expect(players.map((p) => p.tone)).toEqual(['vip', 'active', 'debt']);
    expect(players.map((p) => p.name)).toEqual(['Madina S.', 'Amir K.', 'Olim K.']);
    expect(players.every((p) => p.source === 'fixture')).toBe(true);
  });
});

describe('projectPlayerClient', () => {
  it('derives status/tone from debt and package counts', () => {
    const debtor = projectPlayerClient(
      { playerAccountId: 'p1', displayName: 'Olim', walletBalanceMinorUnits: 0, debtBalanceMinorUnits: 3500, activePackageCount: 0, isActive: true },
      t
    );
    expect(debtor.status).toBe('debt');
    expect(debtor.tone).toBe('debt');
    expect(debtor.debtMinorUnits).toBe(3500);
    expect(debtor.source).toBe('backend');

    const withPackages = projectPlayerClient(
      { playerAccountId: 'p2', displayName: 'Madina', walletBalanceMinorUnits: 46000, debtBalanceMinorUnits: 0, activePackageCount: 2, isActive: true },
      t
    );
    expect(withPackages.status).toBe('package');
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
  it('buildClientSegments returns four stable-id segments with correct counts', () => {
    const clients = [
      client({ tone: 'vip', status: 'package' }),
      client({ debtMinorUnits: 3500, status: 'debt' }),
      client({ status: 'inactive', tone: 'regular' }),
      client({ status: 'active' })
    ];
    const segments = buildClientSegments(clients, t);
    expect(segments.map((s) => s.id)).toEqual(['all', 'vip', 'debt', 'inactive']);
    const byId = (id: ClientSegmentId) => segments.find((s) => s.id === id)!;
    expect(byId('all').count).toBe(4);
    expect(byId('vip').count).toBe(1);
    expect(byId('debt').count).toBe(1);
    expect(byId('inactive').count).toBe(1);
    expect(byId('all').label).toBe('op.players.segments.all');
  });

  it('matchesSegment filters by real fields', () => {
    expect(matchesSegment(client({ tone: 'vip' }), 'all')).toBe(true);
    expect(matchesSegment(client({ tone: 'vip' }), 'vip')).toBe(true);
    expect(matchesSegment(client({ tone: 'active' }), 'vip')).toBe(false);
    expect(matchesSegment(client({ debtMinorUnits: 100 }), 'debt')).toBe(true);
    expect(matchesSegment(client({ debtMinorUnits: 0 }), 'debt')).toBe(false);
    expect(matchesSegment(client({ status: 'inactive' }), 'inactive')).toBe(true);
    expect(matchesSegment(client({ status: 'active' }), 'inactive')).toBe(false);
  });
});
