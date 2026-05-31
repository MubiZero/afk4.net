import { it, expect } from 'bun:test';
import type { PackageOption } from '@/api/types';
import {
  toPackageRows, buildCreatePackageRequest, buildUpdatePackageRequest, type PackageFormValues
} from './packagesModel';

const option: PackageOption = {
  packageDefinitionId: 'pk1', name: 'Старт', currencyCode: 'RUB', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

const form: PackageFormValues = {
  name: '  Старт  ', currencyCode: 'RUB', price: 600, includedMinutes: 60, bonusMinutes: 10, expiresAfterDays: 30
};

it('maps options to rows: price to major units, seconds to minutes', () => {
  expect(toPackageRows([option])[0]).toEqual({
    packageDefinitionId: 'pk1', name: 'Старт', currencyCode: 'RUB',
    price: 500, includedMinutes: 60, bonusMinutes: 10, expiresAfterDays: 30
  });
});

it('builds a create request: price to minor units, minutes to seconds, trims name', () => {
  expect(buildCreatePackageRequest('org', form, 'idem')).toEqual({
    organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 60000 },
    includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30, idempotencyKey: 'idem'
  });
});

it('builds an update request with isActive', () => {
  expect(buildUpdatePackageRequest('org', form, false)).toEqual({
    organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 60000 },
    includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30, isActive: false
  });
});
