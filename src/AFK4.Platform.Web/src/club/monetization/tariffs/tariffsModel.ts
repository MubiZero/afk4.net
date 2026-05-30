import type {
  CreateTariffRequest, CreateTariffVersionRequest, TariffOption,
  UpdateTariffRequest, UpdateTariffVersionRequest
} from '@/api/types';
import { majorToMinor, minorToMajor } from '../../money';

export interface TariffRow {
  tariffId: string;
  tariffVersionId: string;
  name: string;
  currencyCode: string;
  pricePerMinute: number; // major units, for display
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: string;
  versionNumber: number;
}

export interface TariffFormValues {
  name: string;
  currencyCode: string;
  pricePerMinute: number; // major units, as entered
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
}

export function toTariffRows(options: TariffOption[]): TariffRow[] {
  return options.map(o => ({
    tariffId: o.tariffId,
    tariffVersionId: o.tariffVersionId,
    name: o.name,
    currencyCode: o.currencyCode,
    pricePerMinute: minorToMajor(o.pricePerMinuteMinorUnits),
    minimumBillableMinutes: o.minimumBillableMinutes,
    roundingIncrementMinutes: o.roundingIncrementMinutes,
    effectiveFromUtc: o.effectiveFromUtc,
    versionNumber: o.versionNumber
  }));
}

export function buildCreateTariffRequest(organizationId: string, name: string, idempotencyKey: string): CreateTariffRequest {
  return { organizationId, name: name.trim(), idempotencyKey };
}

export function buildCreateVersionRequest(
  organizationId: string, tariffId: string, form: TariffFormValues, effectiveFromUtc: string, idempotencyKey: string
): CreateTariffVersionRequest {
  return {
    organizationId,
    tariffId,
    currencyCode: form.currencyCode,
    pricePerMinuteMinorUnits: majorToMinor(form.pricePerMinute),
    minimumBillableMinutes: form.minimumBillableMinutes,
    roundingIncrementMinutes: form.roundingIncrementMinutes,
    effectiveFromUtc,
    idempotencyKey
  };
}

export function buildUpdateTariffRequest(organizationId: string, name: string, isActive: boolean): UpdateTariffRequest {
  return { organizationId, name: name.trim(), isActive };
}

export function buildUpdateVersionRequest(
  organizationId: string, form: TariffFormValues, effectiveFromUtc: string, isActive: boolean
): UpdateTariffVersionRequest {
  return {
    organizationId,
    currencyCode: form.currencyCode,
    pricePerMinuteMinorUnits: majorToMinor(form.pricePerMinute),
    minimumBillableMinutes: form.minimumBillableMinutes,
    roundingIncrementMinutes: form.roundingIncrementMinutes,
    effectiveFromUtc,
    isActive
  };
}
