import type { PackageOption, PlayerPackage, PurchasePackageRequest } from '@/api/types';

export interface PlayerPackageRow {
  playerPackageId: string;
  name: string;
  remainingIncludedMinutes: number;
  remainingBonusMinutes: number;
  expiresAtUtc: string | null;
}

export interface PackageChoice {
  packageDefinitionId: string;
  name: string;
}

export function toPlayerPackageRows(packages: PlayerPackage[]): PlayerPackageRow[] {
  return packages.map(p => ({
    playerPackageId: p.playerPackageId,
    name: p.name,
    remainingIncludedMinutes: Math.round(p.remainingIncludedSeconds / 60),
    remainingBonusMinutes: Math.round(p.remainingBonusSeconds / 60),
    expiresAtUtc: p.expiresAtUtc
  }));
}

export function toPackageChoices(options: PackageOption[]): PackageChoice[] {
  return options.map(o => ({ packageDefinitionId: o.packageDefinitionId, name: o.name }));
}

export function buildPurchasePackageRequest(
  organizationId: string,
  packageDefinitionId: string,
  idempotencyKey: string
): PurchasePackageRequest {
  return { organizationId, packageDefinitionId, idempotencyKey };
}
