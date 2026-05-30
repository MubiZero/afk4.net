import { it, expect } from 'vitest';
import { resolvePlatformRoute } from './App';

it('resolves /club/venue to clubVenue', () => {
  expect(resolvePlatformRoute('/club/venue', null, '', 'club').route).toEqual({ kind: 'clubVenue' });
});

it('still resolves the legacy devices route', () => {
  expect(resolvePlatformRoute('/club/branches/b1/devices', null, '', 'club').route)
    .toEqual({ kind: 'clubBranchDevices', branchId: 'b1' });
});
