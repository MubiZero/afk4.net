import { it, expect } from 'bun:test';
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

it('resolves /admin/profile to adminProfile', () => {
  expect(resolvePlatformRoute('/admin/profile', null, '', 'admin').route).toEqual({ kind: 'adminProfile' });
});
