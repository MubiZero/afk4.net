import { useEffect, useRef, useState } from 'react';
import type { TenantBrandingDto } from '../api/types';
import { resolveTenantKey } from './resolveTenantKey';
import { applyTheme } from './applyTheme';
import { fetchTenantBranding } from '../api/brandingApi';

interface UseBrandingOptions {
  hostname: string;
  search: string;
  baseUrl: string;
  fallbackOrganizationId?: string;
  fetchBranding?: (baseUrl: string, tenantKey: string) => Promise<TenantBrandingDto | null>;
  applyThemeImpl?: (branding: TenantBrandingDto | null) => void;
}

export type BrandingState =
  | { status: 'loading' }
  | { status: 'ready'; organizationId: string; brandName: string; logoUrl: string | null };

export function useBranding(options: UseBrandingOptions): BrandingState {
  const [state, setState] = useState<BrandingState>({ status: 'loading' });
  // Keep the ref current so a consumer passing a dynamic option isn't stuck with the mount-time value.
  const opts = useRef(options);
  opts.current = options;

  useEffect(() => {
    let cancelled = false;
    const { hostname, search, baseUrl, fallbackOrganizationId } = opts.current;
    const fetchBranding = opts.current.fetchBranding ?? fetchTenantBranding;
    const apply = opts.current.applyThemeImpl ?? applyTheme;

    async function bootstrap() {
      const key = resolveTenantKey(hostname, search);
      let branding: TenantBrandingDto | null = null;
      if (key) {
        try {
          branding = await fetchBranding(baseUrl, key);
        } catch {
          branding = null;
        }
      }
      if (cancelled) return;
      // Theme is cosmetic — a throw here (e.g. no DOM) must never strand the app on the spinner.
      try { apply(branding); } catch { /* fall through to ready with default theme */ }
      setState({
        status: 'ready',
        organizationId: branding?.organizationId ?? fallbackOrganizationId ?? '',
        brandName: branding?.name ?? 'AFK4',
        logoUrl: branding?.logoUrl ?? null,
      });
    }

    void bootstrap();
    return () => { cancelled = true; };
  }, []);

  return state;
}
