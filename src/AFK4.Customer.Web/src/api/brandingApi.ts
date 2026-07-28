import type { OrganizationBrandingDto } from './types';

export async function fetchOrganizationBranding(
  baseUrl: string,
  organizationKey: string,
  fetchImpl: typeof fetch = fetch
): Promise<OrganizationBrandingDto | null> {
  const response = await fetchImpl(`${baseUrl.replace(/\/$/, '')}/api/public/organization/${encodeURIComponent(organizationKey)}/branding`, {
    method: 'GET'
  });
  if (!response.ok) return null;
  return JSON.parse(await response.text()) as OrganizationBrandingDto;
}
