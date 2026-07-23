import { test, expect } from 'bun:test';
import { makeAccessTokenProvider } from './operatorHelpers';
import type { OperatorAuthSession } from './authClient';

test('provider refreshes expired token', async () => {
  const expired = {
    accessToken: 'old', accessTokenExpiresAtUtc: '2000-01-01T00:00:00Z',
    organizationId: 'o', refreshToken: 'r'
  } as unknown as OperatorAuthSession;
  let refreshed = false;
  const provider = makeAccessTokenProvider(expired, {
    isExpired: () => true,
    refresh: async () => { refreshed = true; return { ...expired, accessToken: 'new' }; }
  });
  expect(await provider()).toBe('new');
  expect(refreshed).toBe(true);
});
