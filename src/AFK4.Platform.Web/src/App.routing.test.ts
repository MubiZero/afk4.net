import { it, expect } from 'vitest';
import { resolvePlatformRoute } from './App';

it('resolves /club/venue to clubVenue', () => {
  expect(resolvePlatformRoute('/club/venue', null, '', 'club').route).toEqual({ kind: 'clubVenue' });
});
