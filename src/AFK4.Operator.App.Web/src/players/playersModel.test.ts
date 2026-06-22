import { describe, expect, it } from 'bun:test';
import { fixturePlayers, playerStatusLabel, projectPlayerClient } from './playersModel';
import type { TFunc } from '../operatorHelpers';

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
