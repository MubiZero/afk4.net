import { describe, expect, it, vi } from 'vitest';
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
    const fetchImpl = vi.fn(function (this: unknown) {
      expect(this).toBe(globalThis);
      return Promise.resolve(jsonResponse({ ok: true }));
    });
    vi.stubGlobal('fetch', fetchImpl);

    try {
      const api = new PlatformApiClient({
        baseUrl: 'http://localhost:5074',
        getAccessToken: () => 'access-token'
      });

      await expect(api.get('/api/example')).resolves.toEqual({ ok: true });
      expect(fetchImpl).toHaveBeenCalledTimes(1);
    } finally {
      vi.unstubAllGlobals();
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
    const fetchImpl = vi.fn(async () => new Response('', { status: 404, statusText: 'Not Found' }));
    const api = new PlatformApiClient({
      baseUrl: 'http://localhost:5074',
      getAccessToken: () => 'access-token',
      fetchImpl
    });

    await expect(api.getOptional('/api/branches/branch-1/shifts/current')).resolves.toBeNull();
  });

  it('projects failed responses into PlatformApiError', async () => {
    const fetchImpl = vi.fn(async () => new Response('denied', { status: 403, statusText: 'Forbidden' }));
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
    const fetchImpl = vi.fn();
    const api = new PlatformApiClient({
      baseUrl: 'http://localhost:5074',
      getAccessToken: () => null,
      fetchImpl
    });

    await expect(api.get('/api/branches/branch-1/floor-map')).rejects.toThrow('Operator access token is missing.');
    expect(fetchImpl).not.toHaveBeenCalled();
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
