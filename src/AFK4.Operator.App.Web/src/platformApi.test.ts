import { describe, expect, it, mock } from 'bun:test';
const originalFetch = globalThis.fetch;
import { PlatformApiClient, PlatformApiError } from './platformApi';

describe('PlatformApiClient', () => {
  it('attaches the native access token and reads JSON responses', async () => {
    const calls: Array<[RequestInfo | URL, RequestInit | undefined]> = [];
    const fetchImpl = async (input: RequestInfo | URL, init?: RequestInit) => {
      calls.push([input, init]);
      return jsonResponse({ branchName: 'AFK4 Dushanbe' });
    };
    const api = new PlatformApiClient({
      baseUrl: 'https://afk4.staging.mubi.dev/',
      getAccessToken: () => 'access-token',
      fetchImpl
    });

    await expect(api.get<{ branchName: string }>('/api/branches/branch-1/floor-map')).resolves.toEqual({
      branchName: 'AFK4 Dushanbe'
    });
    expect(calls[0][0]).toBe('https://afk4.staging.mubi.dev/api/branches/branch-1/floor-map');
    expect(calls[0][1]?.method).toBe('GET');
    const headers = calls[0][1]?.headers as Headers;
    expect(headers.get('Authorization')).toBe('Bearer access-token');
  });

  it('calls browser fetch through the global receiver when no override is provided', async () => {
    const fetchImpl = mock(function (this: unknown) {
      expect(this).toBe(globalThis);
      return Promise.resolve(jsonResponse({ ok: true }));
    });
    globalThis.fetch = fetchImpl as unknown as typeof fetch;

    try {
      const api = new PlatformApiClient({
        baseUrl: 'http://localhost:5074',
        getAccessToken: () => 'access-token'
      });

      await expect(api.get('/api/example')).resolves.toEqual({ ok: true });
      expect(fetchImpl).toHaveBeenCalledTimes(1);
    } finally {
      globalThis.fetch = originalFetch;
    }
  });

  it('serializes JSON POST bodies', async () => {
    const calls: Array<[RequestInfo | URL, RequestInit | undefined]> = [];
    const fetchImpl = async (input: RequestInfo | URL, init?: RequestInit) => {
      calls.push([input, init]);
      return jsonResponse({ ok: true });
    };
    const api = new PlatformApiClient({
      baseUrl: 'http://localhost:5074',
      getAccessToken: () => 'access-token',
      fetchImpl
    });

    await api.post('/api/example', { idempotencyKey: 'idem-1' });

    const init = calls[0][1];
    expect(init?.method).toBe('POST');
    expect((init?.headers as Headers).get('Content-Type')).toBe('application/json');
    expect(init?.body).toBe('{"idempotencyKey":"idem-1"}');
  });

  it('builds escaped query strings and reads CSV text', async () => {
    const calls: Array<[RequestInfo | URL, RequestInit | undefined]> = [];
    const fetchImpl = async (input: RequestInfo | URL, init?: RequestInit) => {
      calls.push([input, init]);
      return textResponse('a,b');
    };
    const api = new PlatformApiClient({
      baseUrl: 'https://afk4.staging.mubi.dev/platform/',
      getAccessToken: () => 'access-token',
      fetchImpl
    });

    await expect(api.getText('/api/report.csv', {
      fromUtc: '2026-05-21T10:00:00.000Z',
      query: 'Amir K',
      skip: null
    })).resolves.toBe('a,b');

    expect(calls[0][0]).toBe(
      'https://afk4.staging.mubi.dev/api/report.csv?fromUtc=2026-05-21T10%3A00%3A00.000Z&query=Amir+K'
    );
  });

  it('returns null for optional 404 responses', async () => {
    const fetchImpl = mock(async () => new Response('', { status: 404, statusText: 'Not Found' }));
    const api = new PlatformApiClient({
      baseUrl: 'http://localhost:5074',
      getAccessToken: () => 'access-token',
      fetchImpl
    });

    await expect(api.getOptional('/api/branches/branch-1/shifts/current')).resolves.toBeNull();
  });

  it('projects failed responses into PlatformApiError', async () => {
    const fetchImpl = mock(async () => new Response('denied', { status: 403, statusText: 'Forbidden' }));
    const api = new PlatformApiClient({
      baseUrl: 'http://localhost:5074',
      getAccessToken: () => 'access-token',
      fetchImpl
    });

    await expect(api.get('/api/branches/branch-1/floor-map')).rejects.toMatchObject({
      name: 'PlatformApiError',
      status: 403,
      body: 'denied'
    } satisfies Partial<PlatformApiError>);
  });

  it('fails before network use when the native token is missing', async () => {
    const fetchImpl = mock();
    const api = new PlatformApiClient({
      baseUrl: 'http://localhost:5074',
      getAccessToken: () => null,
      fetchImpl
    });

    await expect(api.get('/api/branches/branch-1/floor-map')).rejects.toThrow('Operator access token is missing.');
    expect(fetchImpl).not.toHaveBeenCalled();
  });
});

describe('PlatformApiClient layout save support', () => {
  function clientWith(fetchImpl: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>) {
    return new PlatformApiClient({ baseUrl: 'https://api.test/', getAccessToken: () => 'tok', fetchImpl });
  }

  it('getWithEtag returns parsed body and the ETag header', async () => {
    const client = clientWith(async () =>
      new Response(JSON.stringify({ ok: true }), { status: 200, headers: { ETag: 'W/"v1"' } }));
    const result = await client.getWithEtag<{ ok: boolean }>('/x');
    expect(result.value.ok).toBe(true);
    expect(result.etag).toBe('W/"v1"');
  });

  it('put sends the If-Match header and JSON body', async () => {
    let seen: Request | null = null;
    const client = clientWith(async (input, init) => {
      seen = new Request(input, init);
      return new Response(JSON.stringify({ eTag: 'W/"v2"' }), { status: 200 });
    });
    const body = await client.put<{ eTag: string }, { a: number }>('/x', { a: 1 }, { ifMatch: 'W/"v1"' });
    expect(body.eTag).toBe('W/"v2"');
    expect(seen!.method).toBe('PUT');
    expect(seen!.headers.get('If-Match')).toBe('W/"v1"');
  });
});

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: {
      'Content-Type': 'application/json'
    }
  });
}

function textResponse(body: string) {
  return new Response(body, {
    status: 200,
    headers: {
      'Content-Type': 'text/csv'
    }
  });
}
