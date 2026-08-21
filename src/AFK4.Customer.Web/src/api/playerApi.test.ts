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
  platformPersonId: 'person1', playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф',
  phoneVerified: true, profileCompleted: true,
  accessToken: 'tok', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  refreshToken: 'ref', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
};

// Клуб в просьбе прислать код не называется: человек входит номером, а не карточкой,
// заведённой конкретным клубом.
it('startSignIn просит код на номер и не называет клуб', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ expiresInSeconds: 300, resendAfterSeconds: 60 }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session: null, onSessionChanged: () => {}, organizationId: 'org1' });
  await client.startSignIn({ phoneNumber: '+992900000001' });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/public/register/start');
  expect(init.method).toBe('POST');
  expect(JSON.parse(init.body)).toEqual({ phoneNumber: '+992900000001' });
});

it('confirmSignIn обменивает код на сессию человека', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ platformPersonId: 'person1', accessToken: 'a', profileCompleted: false }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session: null, onSessionChanged: () => {} });
  const result = await client.confirmSignIn({ phoneNumber: '+992900000001', code: '1234' });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/public/register/confirm');
  expect(JSON.parse(init.body)).toEqual({ phoneNumber: '+992900000001', code: '1234' });
  expect(result.profileCompleted).toBe(false);
});

// Токен принадлежит человеку, а клубов у человека может быть несколько — клуб этой сборки
// называется заголовком, иначе кошелёк показался бы не тот, чей сайт человек открыл.
it('запросы под токеном называют клуб заголовком', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ items: [], nextCursor: null }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {}, organizationId: 'org-77' });
  await client.getVisits();
  expect(fetchImpl.mock.calls[0][1].headers['X-AFK4-Organization']).toBe('org-77');
});

it('без известного клуба заголовок не подставляется', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ person: {}, clubs: [] }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.getMe();
  expect(fetchImpl.mock.calls[0][0]).toBe('https://api.test/api/me');
  expect(fetchImpl.mock.calls[0][1].headers['X-AFK4-Organization']).toBeUndefined();
});

// PIN отвечает 204 без тела: разбирать пустую строку как JSON значило бы уронить вызов ровно
// там, где сервер сказал «сделано».
it('setPin переживает ответ без тела', async () => {
  const fetchImpl = mock().mockResolvedValue({ ok: true, status: 204, headers: new Map(), text: async () => '' } as unknown as Response);
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.setPin('123456');
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/me/pin');
  expect(init.method).toBe('PUT');
  expect(JSON.parse(init.body)).toEqual({ pin: '123456' });
});

it('getBookingRules спрашивает правила конкретного филиала', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ branchId: 'b1', acceptanceMode: 'manual' }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.getBookingRules('b1');
  expect(fetchImpl.mock.calls[0][0]).toBe('https://api.test/api/me/branches/b1/booking-rules');
});

it('getDashboard attaches the Bearer header', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ walletBalance: { currencyCode: 'TJS', minorUnits: 0 }, heldBalance: { currencyCode: 'TJS', minorUnits: 0 }, debtBalance: { currencyCode: 'TJS', minorUnits: 0 }, activeSession: null }));
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
    .mockResolvedValueOnce(okJson({ accessToken: 'tok2', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z', refreshToken: 'ref2', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z', playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф', phoneVerified: true, platformPersonId: 'person1', profileCompleted: true }))
    .mockResolvedValueOnce(okJson({ walletBalance: { currencyCode: 'TJS', minorUnits: 0 }, heldBalance: { currencyCode: 'TJS', minorUnits: 0 }, debtBalance: { currencyCode: 'TJS', minorUnits: 0 }, activeSession: null }));
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
    .mockResolvedValueOnce(okJson({ accessToken: 'tok2', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z', refreshToken: 'ref2', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z', playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф', phoneVerified: true, platformPersonId: 'person1', profileCompleted: true }))
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

it('getFeatures rejects when the 200 body has no proper features array', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({}));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await expect(client.getFeatures()).rejects.toThrow();
});

it('surfaces the 403 D8 gate as a PlayerApiError with status 403', async () => {
  const fetchImpl = mock().mockResolvedValue(status(403));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await expect(client.createReservation({ startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T11:00:00Z' }))
    .rejects.toMatchObject({ status: 403 });
});
