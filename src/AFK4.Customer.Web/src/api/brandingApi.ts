import type { TenantBrandingDto } from './types';

export async function fetchTenantBranding(
  baseUrl: string,
  tenantKey: string,
  fetchImpl: typeof fetch = fetch
): Promise<TenantBrandingDto | null> {
  const response = await fetchImpl(`${baseUrl.replace(/\/$/, '')}/api/public/tenant/${encodeURIComponent(tenantKey)}/branding`, {
    method: 'GET'
  });
  if (!response.ok) return null;
  return JSON.parse(await response.text()) as TenantBrandingDto;
}
