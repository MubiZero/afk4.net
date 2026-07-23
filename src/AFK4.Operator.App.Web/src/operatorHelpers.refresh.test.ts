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

test('concurrent calls dedup into a single refresh', async () => {
  const expired = {
    accessToken: 'old', accessTokenExpiresAtUtc: '2000-01-01T00:00:00Z',
    organizationId: 'o', refreshToken: 'r'
  } as unknown as OperatorAuthSession;
  let refreshCalls = 0;
  const provider = makeAccessTokenProvider(expired, {
    isExpired: () => true,
    refresh: async () => {
      refreshCalls += 1;
      // Yield a tick so both calls to provider() land inside the same in-flight window.
      await Promise.resolve();
      return { ...expired, accessToken: 'new' };
    }
  });

  const [first, second] = await Promise.all([provider(), provider()]);

  expect(refreshCalls).toBe(1);
  expect(first).toBe('new');
  expect(second).toBe('new');
});

test('provider retries refresh after a transient failure', async () => {
  const expired = {
    accessToken: 'old', accessTokenExpiresAtUtc: '2000-01-01T00:00:00Z',
    organizationId: 'o', refreshToken: 'r'
  } as unknown as OperatorAuthSession;
  let attempt = 0;
  const provider = makeAccessTokenProvider(expired, {
    isExpired: () => true,
    refresh: async () => {
      attempt += 1;
      if (attempt === 1) {
        throw new Error('transient refresh failure');
      }
      return { ...expired, accessToken: 'new' };
    }
  });

  await expect(provider()).rejects.toThrow('transient refresh failure');
  // inFlight must be cleared on rejection, otherwise this second call would replay the same error.
  expect(await provider()).toBe('new');
  expect(attempt).toBe(2);
});
