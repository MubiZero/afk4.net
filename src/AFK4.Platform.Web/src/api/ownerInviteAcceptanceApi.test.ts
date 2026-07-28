import { describe, expect, it, mock } from 'bun:test';
import { OwnerInviteAcceptanceApi } from './ownerInviteAcceptanceApi';

describe('OwnerInviteAcceptanceApi', () => {
  it('accepts an owner invite without storing the returned staff session', async () => {
    const fetchImpl = mock(async (_input: RequestInfo | URL, _init?: RequestInit) => new Response(JSON.stringify({ accessToken: 'must-not-be-stored' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    }));
    const client = new OwnerInviteAcceptanceApi({ baseUrl: 'https://api.test', fetchImpl });

    await client.accept({ code: 'code-1', userName: 'owner@example.test', displayName: '', password: 'Passw0rd!' });

    expect(fetchImpl).toHaveBeenCalledTimes(1);
    expect(fetchImpl.mock.calls[0]?.[0]).toBe('https://api.test/api/platform/owner-invites/accept');
    expect(sessionStorage.length).toBe(0);
  });
});
