import { describe, expect, it, vi } from 'vitest';
import { ClubApiClient } from './clubApi';

describe('getDashboardSummaryForRange', () => {
  it('passes explicit fromUtc/toUtc query params', async () => {
    const fetchImpl = vi.fn().mockResolvedValue(new Response('{}', { status: 200 }));
    const client = new ClubApiClient({ baseUrl: 'http://x', fetchImpl, session: { accessToken: 't', refreshToken: 'r', staffUserId: 's', organizationId: 'o', displayName: 'd', branchIds: ['b'], permissions: [], accessTokenExpiresAtUtc: '', refreshTokenExpiresAtUtc: '' } as never, onSessionChanged: () => {} });
    await client.getDashboardSummaryForRange('b1', '2026-05-20T00:00:00.000Z', '2026-05-20T23:59:59.000Z');
    const url = fetchImpl.mock.calls[0][0] as string;
    expect(url).toContain('/api/branches/b1/dashboard/summary?');
    expect(url).toContain('fromUtc=2026-05-20T00%3A00%3A00.000Z');
    expect(url).toContain('toUtc=2026-05-20T23%3A59%3A59.000Z');
  });
});
