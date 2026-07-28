export interface OperatorConfig {
  runtime: string;
  shellMode: string;
  platformBaseUrl: string;
  currencyCode: string;
  appVersion?: string;
  organizationId?: string;
  branchId?: string;
  setupInstallerUrl?: string; // configured at release; empty => show "obtain from IT" (no broken link)
}

const fallbackConfig: OperatorConfig = {
  runtime: 'browser-dev',
  shellMode: 'vite-dev',
  platformBaseUrl: 'http://localhost:5074/',
  currencyCode: 'TJS',
  appVersion: 'dev'
};

// Plain-browser production build: the WPF host isn't there to inject __AFK4_ORGANIZATION_ADMIN_CONFIG__, so
// the API origin has to come from a build-time env var instead. Set VITE_PLATFORM_BASE_URL when
// building (e.g. `VITE_PLATFORM_BASE_URL=https://<platform-host> bun run build`) — never hardcode a
// real URL here. Missing it in a real production build is a deploy misconfiguration, so this throws
// rather than silently pointing the browser build at localhost.
function browserConfigFromEnv(): OperatorConfig {
  const platformBaseUrl = import.meta.env.VITE_PLATFORM_BASE_URL;
  if (!platformBaseUrl) {
    throw new Error(
      'operatorConfig: VITE_PLATFORM_BASE_URL is not set. The browser build needs it at build time to reach the platform API.'
    );
  }
  return {
    runtime: 'browser',
    shellMode: 'web',
    platformBaseUrl,
    currencyCode: 'TJS',
    setupInstallerUrl: import.meta.env.VITE_SETUP_INSTALLER_URL || undefined
  };
}

export function getOperatorConfig(): OperatorConfig {
  const injected = window.__AFK4_ORGANIZATION_ADMIN_CONFIG__;
  if (injected) {
    return injected;
  }
  // Dev server (vite dev / bun test) keeps the localhost fallback — devHostBridge.ts injects a real
  // dev config anyway, so this only matters before that dynamic import resolves, and in tests.
  return import.meta.env.PROD ? browserConfigFromEnv() : fallbackConfig;
}

declare global {
  interface Window {
    __AFK4_ORGANIZATION_ADMIN_CONFIG__?: OperatorConfig;
  }
}
