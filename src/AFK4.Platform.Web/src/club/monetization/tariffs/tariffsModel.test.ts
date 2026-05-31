import { it, expect } from 'bun:test';
import type { TariffOption } from '@/api/types';
import {
  toTariffRows, buildCreateTariffRequest, buildCreateVersionRequest,
  buildUpdateTariffRequest, buildUpdateVersionRequest, type TariffFormValues
} from './tariffsModel';

const option: TariffOption = {
  tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', tariffRuleVersionId: 'rv1', versionNumber: 1,
  currencyCode: 'RUB', pricePerMinuteMinorUnits: 250, minimumBillableMinutes: 5, roundingIncrementMinutes: 1,
  effectiveFromUtc: '2026-01-01T00:00:00.000Z'
};

const form: TariffFormValues = {
  name: '  Дневной  ', currencyCode: 'RUB', pricePerMinute: 3, minimumBillableMinutes: 5, roundingIncrementMinutes: 1
};

it('maps options to rows with price in major units', () => {
  const rows = toTariffRows([option]);
  expect(rows).toHaveLength(1);
  expect(rows[0]).toMatchObject({ tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', pricePerMinute: 2.5, minimumBillableMinutes: 5 });
});

it('builds a create-tariff request, trimming the name', () => {
  expect(buildCreateTariffRequest('org', '  Дневной ', 'idem')).toEqual({ organizationId: 'org', name: 'Дневной', idempotencyKey: 'idem' });
});

it('builds a create-version request converting price to minor units', () => {
  expect(buildCreateVersionRequest('org', 't1', form, '2026-02-01T00:00:00.000Z', 'idem2')).toEqual({
    organizationId: 'org', tariffId: 't1', currencyCode: 'RUB', pricePerMinuteMinorUnits: 300,
    minimumBillableMinutes: 5, roundingIncrementMinutes: 1, effectiveFromUtc: '2026-02-01T00:00:00.000Z', idempotencyKey: 'idem2'
  });
});

it('builds an update-tariff request', () => {
  expect(buildUpdateTariffRequest('org', ' Ночной ', false)).toEqual({ organizationId: 'org', name: 'Ночной', isActive: false });
});

it('builds an update-version request converting price to minor units', () => {
  expect(buildUpdateVersionRequest('org', form, '2026-02-01T00:00:00.000Z', true)).toEqual({
    organizationId: 'org', currencyCode: 'RUB', pricePerMinuteMinorUnits: 300,
    minimumBillableMinutes: 5, roundingIncrementMinutes: 1, effectiveFromUtc: '2026-02-01T00:00:00.000Z', isActive: true
  });
});
