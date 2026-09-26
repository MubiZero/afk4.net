import { beforeEach, describe, expect, it, mock } from 'bun:test';
import { PlatformApiError, PlatformStaleClientError, PlatformTransport, TransportErrorCodes } from './platformTransport';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function emptyResponse(status: number): Response {
  return new Response('', { status });
}

function sessionBody(overrides: Record<string, unknown> = {}) {
  return {
    platformAdminId: 'a',
    userName: 'u',
    displayName: 'd',
    accessToken: 't',
    accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
    refreshToken: 'r',
    refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z',
    roles: [],
    permissions: [],
    ...overrides
  };
}

describe('PlatformTransport — two-step sign-in', () => {
  beforeEach(() => {
    if (typeof globalThis.sessionStorage !== 'undefined') {
      globalThis.sessionStorage.clear();
    }
  });

  it('signIn returns a challenge and never applies a session', async () => {
    const fetchImpl = mock(async () =>
      jsonResponse(200, { challengeToken: 'chal-1', expiresAtUtc: '2030-01-01T00:02:00Z', twoFactorConfigured: true })
    );
    const onSessionChanged = mock();
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged
    });

    const outcome = await transport.signIn('u', 'p');

    expect(outcome).toEqual({
      kind: 'challenge',
      challengeToken: 'chal-1',
      twoFactorConfigured: true,
      expiresAtUtc: '2030-01-01T00:02:00Z'
    });
    expect(transport.getSession()).toBeNull();
    expect(onSessionChanged).not.toHaveBeenCalled();
  });

  it('beginTwoFactorSetup returns the secret and QR link without touching the session', async () => {
    const fetchImpl = mock(async () =>
      jsonResponse(200, { secret: 'ABCD1234', otpAuthUri: 'otpauth://totp/AFK4?secret=ABCD1234' })
    );
    const onSessionChanged = mock();
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged
    });

    const result = await transport.beginTwoFactorSetup('chal-1');

    expect(result).toEqual({ secret: 'ABCD1234', otpAuthUri: 'otpauth://totp/AFK4?secret=ABCD1234' });
    expect(onSessionChanged).not.toHaveBeenCalled();
  });

  it('completeTwoFactorSetup applies the returned session and surfaces recovery codes once', async () => {
    const fetchImpl = mock(async () =>
      jsonResponse(200, { session: sessionBody({ accessToken: 'fresh-access' }), recoveryCodes: ['code-1', 'code-2'] })
    );
    let observed: unknown = null;
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: next => { observed = next; }
    });

    const result = await transport.completeTwoFactorSetup('chal-1', '123456');

    expect(result.recoveryCodes).toEqual(['code-1', 'code-2']);
    expect(transport.getSession()?.accessToken).toBe('fresh-access');
    expect(observed).not.toBeNull();
  });

  it('completeTwoFactor (verify) applies the returned session', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, sessionBody({ accessToken: 'verified-access' })));
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    const session = await transport.completeTwoFactor('chal-1', '123456');

    expect(session.accessToken).toBe('verified-access');
    expect(transport.getSession()?.accessToken).toBe('verified-access');
  });

  // Regression for the platform-admin-directory-2fa review, point 3: the Platform API and the
  // panel bundle deploy as two independent Coolify apps with no shared artifact, so there's always
  // a window where a stale cached bundle can hit a new API. Before this response is validated, an
  // old-shaped 200 (the pre-2FA full session, no challengeToken) would silently produce a
  // "challenge" with `challengeToken: undefined` instead of failing loudly.
  it('signIn rejects with PlatformStaleClientError when the response is the old pre-2FA session shape', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, sessionBody()));
    const onSessionChanged = mock();
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged
    });

    await expect(transport.signIn('u', 'p')).rejects.toBeInstanceOf(PlatformStaleClientError);
    expect(transport.getSession()).toBeNull();
    expect(onSessionChanged).not.toHaveBeenCalled();
  });

  // Mirror case for step 2: an old API that never grew the challenge/2FA routes could answer
  // /2fa/verify or /2fa/setup/confirm with a body that has no working tokens. That must not be
  // assembled into a session with undefined accessToken/refreshToken.
  it('completeTwoFactor rejects with PlatformStaleClientError when the response carries no working tokens', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, { ...sessionBody(), accessToken: '', refreshToken: '' }));
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await expect(transport.completeTwoFactor('chal-1', '123456')).rejects.toBeInstanceOf(PlatformStaleClientError);
    expect(transport.getSession()).toBeNull();
  });

  it('completeTwoFactorSetup rejects with PlatformStaleClientError when the response carries no working tokens', async () => {
    const fetchImpl = mock(async () =>
      jsonResponse(200, { session: { ...sessionBody(), accessToken: '', refreshToken: '' }, recoveryCodes: ['code-1'] })
    );
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await expect(transport.completeTwoFactorSetup('chal-1', '123456')).rejects.toBeInstanceOf(PlatformStaleClientError);
    expect(transport.getSession()).toBeNull();
  });

  it('rejects with PlatformApiError(429) on lockout and does not apply a session', async () => {
    const fetchImpl = mock(async () => emptyResponse(429));
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await expect(transport.completeTwoFactor('chal-1', '000000')).rejects.toMatchObject({ status: 429 });
    expect(transport.getSession()).toBeNull();
  });
});

describe('PlatformTransport — запрос, который не дождался ответа', () => {
  function neverAnswering(): typeof fetch {
    return (async (_url: string, init?: RequestInit) => new Promise<Response>((_resolve, reject) => {
      init?.signal?.addEventListener('abort', () => reject(init.signal?.reason ?? new Error('aborted')));
    })) as unknown as typeof fetch;
  }

  function transportWithTimeout(fetchImpl: typeof fetch, timeoutMs = 20): PlatformTransport {
    return new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {},
      timeoutMs
    });
  }

  // Без предела вкладка висела на «Сохраняю…», пока браузер сам не оборвёт соединение: кнопка
  // заблокирована, отменить нечем, о судьбе операции ничего не известно.
  it('обрывает запрос по пределу ожидания и называет причину', async () => {
    const transport = transportWithTimeout(neverAnswering());

    await expect(transport.send('GET', '/api/platform/pulse')).rejects.toMatchObject({
      status: 0,
      errorCode: TransportErrorCodes.Timeout
    });
  });

  it('сетевой сбой приходит тем же языком — статусом 0, а не сырым TypeError', async () => {
    const transport = transportWithTimeout((async () => { throw new TypeError('Failed to fetch'); }) as unknown as typeof fetch);

    await expect(transport.send('GET', '/api/platform/pulse')).rejects.toMatchObject({
      status: 0,
      errorCode: TransportErrorCodes.Network
    });
  });

  // Отмена вызывающим — не ошибка: так глобальный поиск снимает устаревший запрос, и подменять
  // это «сервер не ответил» нельзя, иначе экран покажет отказ там, где ничего не случилось.
  it('отмену вызывающим пробрасывает как есть', async () => {
    const transport = transportWithTimeout(neverAnswering(), 10_000);
    const controller = new AbortController();
    const pending = transport.send('GET', '/api/platform/search?query=a', undefined, controller.signal);
    controller.abort();

    await expect(pending).rejects.not.toBeInstanceOf(PlatformApiError);
  });
});

describe('PlatformTransport — продление сессии', () => {
  const live = sessionBody({ accessToken: 'old-access', refreshToken: 'old-refresh' });

  function transportWithSession(fetchImpl: typeof fetch, onSessionChanged: (s: unknown) => void = () => {}) {
    return new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: live as never,
      onSessionChanged: onSessionChanged as never,
      timeoutMs: 200
    });
  }

  // Беда на стороне сервера не должна стоить администратору повторного входа с кодом из телефона:
  // раньше любой неуспешный ответ на продление стирал сессию и выбрасывал на форму входа.
  it('не выкидывает из панели, когда продление упало с ошибкой сервера', async () => {
    const fetchImpl = mock(async (url: string) =>
      url.endsWith('/auth/refresh') ? emptyResponse(503) : emptyResponse(401));
    const transport = transportWithSession(fetchImpl as unknown as typeof fetch);

    await expect(transport.send('GET', '/api/platform/pulse')).rejects.toMatchObject({ status: 401 });
    expect(transport.getSession()).not.toBeNull();
  });

  it('снимает сессию, когда сервер отверг сам токен продления', async () => {
    const fetchImpl = mock(async (url: string) =>
      url.endsWith('/auth/refresh') ? emptyResponse(401) : emptyResponse(401));
    const transport = transportWithSession(fetchImpl as unknown as typeof fetch);

    await expect(transport.send('GET', '/api/platform/pulse')).rejects.toMatchObject({ status: 401 });
    expect(transport.getSession()).toBeNull();
  });
});

describe('PlatformTransport — ключ повторной попытки', () => {
  it('повтор с тем же ключом уходит с тем же заголовком', async () => {
    const seen: string[] = [];
    const fetchImpl = mock(async (_url: string, init?: RequestInit) => {
      seen.push((init?.headers as Record<string, string>)['Idempotency-Key']);
      return jsonResponse(200, { ok: true });
    });
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await transport.sendIdempotent('POST', '/api/platform/organizations', { name: 'a' }, 'attempt-1');
    await transport.sendIdempotent('POST', '/api/platform/organizations', { name: 'a' }, 'attempt-1');

    expect(seen).toEqual(['attempt-1', 'attempt-1']);
  });

  it('без ключа каждая попытка своя — это защищает только от двойного клика', async () => {
    const seen: string[] = [];
    const fetchImpl = mock(async (_url: string, init?: RequestInit) => {
      seen.push((init?.headers as Record<string, string>)['Idempotency-Key']);
      return jsonResponse(200, { ok: true });
    });
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await transport.sendIdempotent('POST', '/api/platform/organizations', { name: 'a' });
    await transport.sendIdempotent('POST', '/api/platform/organizations', { name: 'a' });

    expect(seen[0]).not.toBe(seen[1]);
  });
});

describe('PlatformTransport — загрузка файла', () => {
  // Картинку шлют частями формы: JSON-заголовок сломал бы границу частей, а сервер не нашёл бы файл.
  it('форму отправляет как есть, без JSON-заголовка, но с токеном', async () => {
    let seen: RequestInit | undefined;
    const fetchImpl = mock(async (_url: string, init: RequestInit) => {
      seen = init;
      return jsonResponse(200, { url: 'https://media.test/x.jpg' });
    });
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: sessionBody() as never,
      onSessionChanged: () => {}
    });
    const form = new FormData();
    form.append('purpose', 'catalog-cover');

    await transport.send('POST', '/api/platform/media', form);

    expect(seen?.body).toBe(form);
    const headers = seen?.headers as Record<string, string>;
    expect(headers['Content-Type']).toBeUndefined();
    expect(headers.Authorization).toBe('Bearer t');
  });
});
