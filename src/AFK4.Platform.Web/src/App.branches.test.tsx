import { it, expect } from 'vitest';
import { resolvePlatformRoute, pathForRoute } from './App';

it('resolves /club/branches to the clubBranches route', () => {
  const { route } = resolvePlatformRoute('/club/branches', null, '', 'club');
  expect(route).toEqual({ kind: 'clubBranches' });
});

it('maps the clubBranches route back to /club/branches', () => {
  expect(pathForRoute({ kind: 'clubBranches' })).toBe('/club/branches');
});
