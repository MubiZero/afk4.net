import { afterEach, describe, expect, it, mock } from 'bun:test';
import { PlatformApiClient } from './platformApi';
import { withCriticalUpdateActivity } from './updateActivity';

describe('withCriticalUpdateActivity', () => {
  afterEach(() => {
    delete window.chrome;
    mock.restore();
  });

  it('tracks overlapping critical mutations until each request settles', async () => {
    const messages: Array<{ type: string; operationId: string }> = [];
    const resolvers: Array<(response: Response) => void> = [];
    window.chrome = { webview: { postMessage: (message) => messages.push(message as typeof messages[number]) } };
    const api = withCriticalUpdateActivity(createApi(() => new Promise(resolve => resolvers.push(resolve))));

    const session = api.post('sessions/session-1/end', {});
    const payment = api.post('pos/sales/sale-1/payments/manual', {});
    await Promise.resolve();

    expect(messages.map(message => message.type)).toEqual(['update-activity:start', 'update-activity:start']);
    expect(new Set(messages.map(message => message.operationId)).size).toBe(2);
    resolvers[0](jsonResponse());
    await session;
    expect(messages.map(message => message.type)).toEqual([
      'update-activity:start', 'update-activity:start', 'update-activity:finish'
    ]);
    resolvers[1](jsonResponse());
    await payment;
    expect(messages.map(message => message.type)).toEqual([
      'update-activity:start', 'update-activity:start', 'update-activity:finish', 'update-activity:finish'
    ]);
  });

  it('finishes activity when a critical mutation fails', async () => {
    const messages: Array<{ type: string }> = [];
    window.chrome = { webview: { postMessage: (message) => messages.push(message as { type: string }) } };
    const api = withCriticalUpdateActivity(createApi(async () => { throw new Error('offline'); }));

    await expect(api.post('branches/branch-1/shifts/open', {})).rejects.toThrow('offline');

    expect(messages.map(message => message.type)).toEqual(['update-activity:start', 'update-activity:finish']);
  });

  it('does not track reads or mutations outside the explicit critical allowlist', async () => {
    const postMessage = mock();
    window.chrome = { webview: { postMessage } };
    const api = withCriticalUpdateActivity(createApi(async () => jsonResponse()));

    await api.get('branches/branch-1/sessions');
    await api.post('branches/branch-1/news', {});

    expect(postMessage).not.toHaveBeenCalled();
  });

  it.each([
    'branches/branch-1/sessions/start',
    'pos/sales/sale-1/refunds',
    'devices/device-1/commands',
    'branches/branch-1/shifts/open',
    'branches/branch-1/money-actions/request-1/approve',
    'players/player-1/wallet/top-ups',
    'players/player-1/debts/payments',
    'players/player-1/ledger/manual-corrections',
    'players/player-1/ledger/entry-1/refunds',
    'players/player-1/packages/purchases'
  ])('classifies %s as critical', async path => {
    const postMessage = mock();
    window.chrome = { webview: { postMessage } };
    const api = withCriticalUpdateActivity(createApi(async () => jsonResponse()));

    await api.post(path, {});

    expect(postMessage).toHaveBeenCalledTimes(2);
  });
});

function createApi(fetchImpl: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>): PlatformApiClient {
  return new PlatformApiClient({
    baseUrl: 'https://api.test/',
    getAccessToken: () => 'token',
    fetchImpl
  });
}

function jsonResponse(): Response {
  return new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } });
}
