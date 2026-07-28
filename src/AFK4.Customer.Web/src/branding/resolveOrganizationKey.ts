const TENANT_KEY_STORAGE = 'afk4.player.organizationKey';
// Hosts that are not a organization subdomain (local dev, bare apex, the portal host itself).
const NON_TENANT_HOSTS = new Set(['localhost', '127.0.0.1', 'portal', 'www']);

export function resolveOrganizationKey(
  hostname: string,
  search: string,
  storage: Storage | null = (globalThis as { localStorage?: Storage }).localStorage ?? null
): string | null {
  const override = new URLSearchParams(search).get('organization');
  if (override) {
    storage?.setItem(TENANT_KEY_STORAGE, override);
    return override;
  }

  const firstLabel = hostname.split('.')[0];
  if (firstLabel && !NON_TENANT_HOSTS.has(firstLabel) && hostname.includes('.')) {
    storage?.setItem(TENANT_KEY_STORAGE, firstLabel);
    return firstLabel;
  }

  return storage?.getItem(TENANT_KEY_STORAGE) ?? null;
}
