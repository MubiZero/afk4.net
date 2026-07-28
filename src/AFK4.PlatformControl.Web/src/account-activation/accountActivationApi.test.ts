import { describe, expect, it, mock } from 'bun:test';
import { AccountActivationApi } from './accountActivationApi';

describe('AccountActivationApi', () => {
  it('accepts an account activation without storing the returned staff session', async () => {
    const fetchImpl = mock(async (_input: RequestInfo | URL, _init?: RequestInit) => new Response(JSON.stringify({ accessToken: 'must-not-be-stored' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    }));
    const client = new AccountActivationApi({ baseUrl: 'https://api.test', fetchImpl });

    await client.accept({ code: 'code-1', userName: 'owner@example.test', displayName: '', password: 'Passw0rd!' });

    expect(fetchImpl).toHaveBeenCalledTimes(1);
    expect(fetchImpl.mock.calls[0]?.[0]).toBe('https://api.test/api/account-activation/organization-owner');
    expect(sessionStorage.length).toBe(0);
  });
});
