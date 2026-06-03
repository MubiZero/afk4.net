import { it, expect, mock } from 'bun:test';
import { fetchTenantBranding } from './brandingApi';

function okJson(body: unknown): Response {
  return { ok: true, status: 200, headers: new Map(), text: async () => JSON.stringify(body) } as unknown as Response;
}
function notFound(): Response {
  return { ok: false, status: 404, headers: new Map(), text: async () => '' } as unknown as Response;
}

it('GETs the branding endpoint for the tenant key', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ organizationId: 'org1', name: 'CyberX', logoUrl: null, accentColor: '#c8ff00' }));
  const result = await fetchTenantBranding('https://api.test', 'cyberx', fetchImpl as unknown as typeof fetch);
  expect(fetchImpl.mock.calls[0][0]).toBe('https://api.test/api/public/tenant/cyberx/branding');
  expect(result?.name).toBe('CyberX');
});

it('returns null on 404 (unknown tenant)', async () => {
  const fetchImpl = mock().mockResolvedValue(notFound());
  expect(await fetchTenantBranding('https://api.test', 'nope', fetchImpl as unknown as typeof fetch)).toBeNull();
});
