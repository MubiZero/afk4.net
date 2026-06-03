import { it, expect, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import { useBranding } from './useBranding';

it('resolves a tenant key, fetches branding and reports ready', async () => {
  const fetchBranding = mock().mockResolvedValue({ organizationId: 'org-9', name: 'Cyber Arena', logoUrl: null, accentColor: '#ff0080' });
  const apply = mock();
  const { result } = renderHook(() => useBranding({
    hostname: 'cyber.portal.afk4.net', search: '', baseUrl: '', fetchBranding, applyThemeImpl: apply
  }));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.organizationId).toBe('org-9');
  expect(result.current.brandName).toBe('Cyber Arena');
  expect(apply).toHaveBeenCalledTimes(1);
});

it('falls back to defaults when no branding resolves', async () => {
  const fetchBranding = mock().mockResolvedValue(null);
  const { result } = renderHook(() => useBranding({
    hostname: 'localhost', search: '', baseUrl: '', fallbackOrganizationId: 'dev-org',
    fetchBranding, applyThemeImpl: mock()
  }));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.organizationId).toBe('dev-org');
  expect(result.current.brandName).toBe('AFK4');
});
