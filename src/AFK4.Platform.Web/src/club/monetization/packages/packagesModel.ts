import type {
  CreatePackageDefinitionRequest, PackageOption, UpdatePackageDefinitionRequest
} from '@/api/types';
import { majorToMinor, minorToMajor } from '../../money';

export interface PackageRow {
  packageDefinitionId: string;
  name: string;
  currencyCode: string;
  price: number; // major units, for display
  includedMinutes: number;
  bonusMinutes: number;
  expiresAfterDays: number;
}

export interface PackageFormValues {
  name: string;
  currencyCode: string;
  price: number; // major units, as entered
  includedMinutes: number;
  bonusMinutes: number;
  expiresAfterDays: number;
}

export function toPackageRows(options: PackageOption[]): PackageRow[] {
  return options.map(o => ({
    packageDefinitionId: o.packageDefinitionId,
    name: o.name,
    currencyCode: o.currencyCode,
    price: minorToMajor(o.priceMinorUnits),
    includedMinutes: Math.round(o.includedSeconds / 60),
    bonusMinutes: Math.round(o.bonusSeconds / 60),
    expiresAfterDays: o.expiresAfterDays
  }));
}

export function buildCreatePackageRequest(organizationId: string, form: PackageFormValues, idempotencyKey: string): CreatePackageDefinitionRequest {
  return {
    organizationId,
    name: form.name.trim(),
    price: { currencyCode: form.currencyCode, minorUnits: majorToMinor(form.price) },
    includedSeconds: Math.round(form.includedMinutes * 60),
    bonusSeconds: Math.round(form.bonusMinutes * 60),
    expiresAfterDays: form.expiresAfterDays,
    idempotencyKey
  };
}

export function buildUpdatePackageRequest(organizationId: string, form: PackageFormValues, isActive: boolean): UpdatePackageDefinitionRequest {
  return {
    organizationId,
    name: form.name.trim(),
    price: { currencyCode: form.currencyCode, minorUnits: majorToMinor(form.price) },
    includedSeconds: Math.round(form.includedMinutes * 60),
    bonusSeconds: Math.round(form.bonusMinutes * 60),
    expiresAfterDays: form.expiresAfterDays,
    isActive
  };
}
