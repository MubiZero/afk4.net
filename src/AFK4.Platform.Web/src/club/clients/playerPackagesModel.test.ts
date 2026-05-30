import { it, expect } from 'vitest';
import type { PackageOption, PlayerPackage } from '@/api/types';
import { toPlayerPackageRows, toPackageChoices, buildPurchasePackageRequest } from './playerPackagesModel';

const pkg: PlayerPackage = {
  playerPackageId: 'pp1', packageDefinitionId: 'pd1', playerAccountId: 'p1', name: 'Старт',
  purchasedPrice: { currencyCode: 'TJS', minorUnits: 50000 },
  includedSeconds: 3600, bonusSeconds: 600, remainingIncludedSeconds: 1800, remainingBonusSeconds: 300,
  purchasedAtUtc: '2026-05-01T00:00:00.000Z', expiresAtUtc: '2026-06-01T00:00:00.000Z'
};

const option: PackageOption = {
  packageDefinitionId: 'pd1', name: 'Старт', currencyCode: 'TJS', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

it('maps player packages to rows: remaining seconds to minutes', () => {
  expect(toPlayerPackageRows([pkg])[0]).toEqual({
    playerPackageId: 'pp1', name: 'Старт',
    remainingIncludedMinutes: 30, remainingBonusMinutes: 5, expiresAtUtc: '2026-06-01T00:00:00.000Z'
  });
});

it('keeps a null expiry as null', () => {
  expect(toPlayerPackageRows([{ ...pkg, expiresAtUtc: null }])[0].expiresAtUtc).toBeNull();
});

it('maps package options to purchase choices', () => {
  expect(toPackageChoices([option])).toEqual([{ packageDefinitionId: 'pd1', name: 'Старт' }]);
});

it('builds a purchase request', () => {
  expect(buildPurchasePackageRequest('org', 'pd1', 'idem')).toEqual({
    organizationId: 'org', packageDefinitionId: 'pd1', idempotencyKey: 'idem'
  });
});
