// src/App.settings.test.tsx
import { it, expect } from 'bun:test';
import { resolvePlatformRoute, pathForRoute } from './App';

it('resolves /club/settings to the clubSettings route', () => {
  const { route } = resolvePlatformRoute('/club/settings', null, '', 'club');
  expect(route).toEqual({ kind: 'clubSettings' });
});

it('maps the clubSettings route back to /club/settings', () => {
  expect(pathForRoute({ kind: 'clubSettings' })).toBe('/club/settings');
});
