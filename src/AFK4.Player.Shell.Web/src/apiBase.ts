// The native shell host injects the runtime platform origin (the SAME one it signs bearer tokens
// for) via window.__AFK4_PLAYER_CONFIG__ before any app script runs. Prefer it so the web layer
// always calls the host's configured API; fall back to the build-time var only for browser dev.
interface PlayerShellConfig {
  platformBaseUrl?: string;
}

declare global {
  interface Window {
    __AFK4_PLAYER_CONFIG__?: PlayerShellConfig;
  }
}

const injected =
  typeof window !== 'undefined' ? window.__AFK4_PLAYER_CONFIG__?.platformBaseUrl : undefined;

export const API_BASE =
  (injected && injected.trim()) ||
  import.meta.env.VITE_PLATFORM_API_BASE_URL ||
  'https://afk4.staging.mubi.dev';
