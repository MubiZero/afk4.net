import { it, expect } from 'vitest';
import { resolvePlatformRoute } from './App';

it('resolves /club/venue to clubVenue', () => {
  expect(resolvePlatformRoute('/club/venue', null, '', 'club').route).toEqual({ kind: 'clubVenue' });
});

it('resolves /admin to adminOverview', () => {
  expect(resolvePlatformRoute('/admin', null, '', 'admin').route).toEqual({ kind: 'adminOverview' });
});

it('resolves /admin/tenants to tenantList', () => {
  expect(resolvePlatformRoute('/admin/tenants', null, '', 'admin').route).toEqual({ kind: 'tenantList' });
});
