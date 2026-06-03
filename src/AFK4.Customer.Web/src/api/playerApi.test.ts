import { it, expect, mock } from 'bun:test';
import { PlayerApiClient, PlayerApiError } from './playerApi';
import type { PlayerSession } from '../auth/playerTokenStore';

function okJson(body: unknown): Response {
  return { ok: true, status: 200, headers: new Map(), text: async () => JSON.stringify(body) } as unknown as Response;
}
function status(code: number, body: unknown = {}): Response {
  return { ok: code < 400, status: code, headers: new Map(), text: async () => JSON.stringify(body) } as unknown as Response;
}

const session: PlayerSession = {
  playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф', phoneVerified: true,
  accessToken: 'tok', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  refreshToken: 'ref', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
};

it('signIn POSTs the request and returns the response', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ playerAccountId: 'p1', accessToken: 'a' }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session: null, onSessionChanged: () => {} });
  await client.signIn({ organizationId: 'org1', phoneNumber: '+992900000001', password: '1234' });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/public/player/sign-in');
  expect(init.method).toBe('POST');
  expect(JSON.parse(init.body)).toEqual({ organizationId: 'org1', phoneNumber: '+992900000001', password: '1234' });
});

it('getDashboard attaches the Bearer header', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ walletBalance: { currencyCode: 'TJS', minorUnits: 0 }, debtBalance: { currencyCode: 'TJS', minorUnits: 0 }, activeSession: null }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.getDashboard();
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/me/dashboard');
  expect(init.headers.Authorization).toBe('Bearer tok');
});

it('refreshes once on 401 then retries with the new token', async () => {
  let updated: PlayerSession | null = null;
  const fetchImpl = mock()
    .mockResolvedValueOnce(status(401))
    .mockResolvedValueOnce(okJson({ accessToken: 'tok2', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z', refreshToken: 'ref2', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z', playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф', phoneVerified: true }))
    .mockResolvedValueOnce(okJson({ walletBalance: { currencyCode: 'TJS', minorUnits: 0 }, debtBalance: { currencyCode: 'TJS', minorUnits: 0 }, activeSession: null }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: (s) => { updated = s; } });
  await client.getDashboard();
  expect(fetchImpl.mock.calls[1][0]).toBe('https://api.test/api/public/player/refresh');
  expect(fetchImpl.mock.calls[2][1].headers.Authorization).toBe('Bearer tok2');
  expect((updated as PlayerSession | null)?.accessToken).toBe('tok2');
});

it('throws PlayerApiError with the parsed message on a non-401 error', async () => {
  const fetchImpl = mock().mockResolvedValue(status(400, { error: 'amount must be positive' }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await expect(client.getDashboard()).rejects.toBeInstanceOf(PlayerApiError);
  await expect(client.getDashboard()).rejects.toThrow('amount must be positive');
});

it('getVisits appends the cursor query and attaches the Bearer header', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ items: [], nextCursor: null }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.getVisits('CURSOR_1');
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/me/visits?cursor=CURSOR_1');
  expect(init.headers.Authorization).toBe('Bearer tok');
});

it('createTopUpIntent POSTs the body with Content-Type and Bearer', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ paymentIntentId: 'i1' }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.createTopUpIntent({ amountMinorUnits: 5000, currencyCode: 'TJS' });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/me/wallet/top-up-intent');
  expect(init.method).toBe('POST');
  expect(init.headers['Content-Type']).toBe('application/json');
  expect(init.headers.Authorization).toBe('Bearer tok');
  expect(JSON.parse(init.body)).toEqual({ amountMinorUnits: 5000, currencyCode: 'TJS' });
});

it('a write refreshes once on 401 and re-sends the body with the new token', async () => {
  const fetchImpl = mock()
    .mockResolvedValueOnce(status(401))
    .mockResolvedValueOnce(okJson({ accessToken: 'tok2', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z', refreshToken: 'ref2', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z', playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф', phoneVerified: true }))
    .mockResolvedValueOnce(okJson({ paymentIntentId: 'i1' }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.createTopUpIntent({ amountMinorUnits: 5000, currencyCode: 'TJS' });
  expect(fetchImpl.mock.calls[1][0]).toBe('https://api.test/api/public/player/refresh');
  expect(fetchImpl.mock.calls[2][1].headers.Authorization).toBe('Bearer tok2');
  expect(JSON.parse(fetchImpl.mock.calls[2][1].body)).toEqual({ amountMinorUnits: 5000, currencyCode: 'TJS' });
});

it('cancelReservation issues a DELETE with no body', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ reservationId: 'r1', state: 'cancelled' }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.cancelReservation('r1');
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/me/reservations/r1');
  expect(init.method).toBe('DELETE');
  expect(init.body).toBeUndefined();
});

it('surfaces the 403 D8 gate as a PlayerApiError with status 403', async () => {
  const fetchImpl = mock().mockResolvedValue(status(403));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await expect(client.createReservation({ startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T11:00:00Z' }))
    .rejects.toMatchObject({ status: 403 });
});
