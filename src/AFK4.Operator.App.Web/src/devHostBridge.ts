// DEV-ONLY host-bridge stub. Emulates the native WebView2 auth bridge so the operator UI can
// sign in from a plain browser against the staging API (proxied by vite). This file is imported
// only via a `import.meta.env.DEV` dynamic import in main.tsx, so it is tree-shaken out of the
// production `bun run build` output and never ships in the MSI.
//
// In the native WebView2 host the real bridge + injected __AFK4_OPERATOR_CONFIG__ already exist;
// installing this stub there would clobber the native bridge and point platformBaseUrl at the
// WebView origin (operator.afk4.local) instead of staging — so it stays inert when running inside
// the host.

import { createMockSession, devMockFetch } from './devMockBackend';

const ORG = '0169044b-2f74-46a7-8e52-7656a39a8f8c';
const BRANCH = 'f77b708c-1dc9-4cb3-9c19-21797f7035fc';

// UI-preview mode (default): serve a fake session + mock platform data so the console renders
// without a backend. Append `?live` to the dev URL to instead proxy sign-in to the staging API.
const PREVIEW_MOCK = !location.search.includes('live');

interface DevBridgeMessage {
  type?: string;
  requestId?: string;
  payload?: { organizationId?: string; userName?: string; password?: string };
}

interface DevWebView {
  postMessage(msg: DevBridgeMessage): void;
  addEventListener(type: string, listener: (event: { data: unknown }) => void): void;
  removeEventListener(type: string, listener: (event: { data: unknown }) => void): void;
}

function readString(record: Record<string, unknown>, key: string): string {
  const value = record[key];
  return typeof value === 'string' ? value : '';
}

export function installDevHostBridge(): void {
  const chrome = (window as { chrome?: { webview?: unknown } }).chrome;
  if (chrome && chrome.webview) {
    return;
  }

  window.__AFK4_OPERATOR_CONFIG__ = {
    runtime: 'browser-dev',
    shellMode: 'vite-dev',
    platformBaseUrl: location.origin + '/',
    currencyCode: 'TJS',
    organizationId: ORG,
    branchId: BRANCH
  };

  const listeners = new Set<(event: { data: unknown }) => void>();
  let refreshToken: string | null = null;

  function emit(requestId: string | undefined, ok: boolean, payload: unknown, error: unknown): void {
    const ev = { data: { type: 'host:response', requestId, ok, payload, error } };
    listeners.forEach((l) => {
      try {
        l(ev);
      } catch {
        // Ignore listener faults; this is a dev-only harness.
      }
    });
  }

  function toSession(response: unknown): unknown {
    const r = (response ?? {}) as Record<string, unknown>;
    const token = r.refreshToken;
    if (typeof token === 'string') {
      refreshToken = token;
    }
    const branchIds = Array.isArray(r.branchIds) ? (r.branchIds as unknown[]) : [];
    return {
      staffUserId: r.staffUserId,
      organizationId: r.organizationId,
      displayName: r.displayName,
      accessToken: r.accessToken,
      accessTokenExpiresAtUtc: r.accessTokenExpiresAtUtc,
      refreshTokenExpiresAtUtc: r.refreshTokenExpiresAtUtc,
      branchIds,
      activeBranchId: branchIds[0] ?? undefined,
      permissions: r.permissions
    };
  }

  async function handle(type: string, payload: DevBridgeMessage['payload']): Promise<unknown> {
    if (PREVIEW_MOCK && type.indexOf('auth:') === 0) {
      // Preview: start on the sign-in screen (loadToken → null); any credentials sign in to the
      // mock session; forgot/reset flows just acknowledge.
      if (type === 'auth:loadToken') return null;
      if (type === 'auth:signOut') return { signedOut: true };
      if (type === 'auth:signIn' || type === 'auth:refresh') return createMockSession();
      return { ok: true };
    }
    if (type === 'auth:loadToken') {
      return null;
    }
    if (type === 'auth:signOut') {
      refreshToken = null;
      return { signedOut: true };
    }
    if (type === 'auth:signIn') {
      const body = {
        organizationId: payload?.organizationId || ORG,
        userName: payload?.userName,
        password: payload?.password
      };
      const res = await fetch('/api/auth/staff/sign-in', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
      });
      if (!res.ok) {
        throw { code: 'sign_in_failed', message: 'HTTP ' + res.status };
      }
      return toSession(await res.json());
    }
    if (type === 'auth:refresh') {
      const res = await fetch('/api/auth/staff/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ organizationId: ORG, refreshToken })
      });
      if (!res.ok) {
        throw { code: 'refresh_failed', message: 'HTTP ' + res.status };
      }
      return toSession(await res.json());
    }
    throw { code: 'unhandled', message: 'dev bridge: unhandled ' + type };
  }

  const win = window as unknown as { chrome: { webview: DevWebView } };
  win.chrome = win.chrome || ({} as { webview: DevWebView });
  win.chrome.webview = {
    postMessage(msg: DevBridgeMessage) {
      if (!msg || !msg.type) {
        return;
      }
      if (msg.type.indexOf('window:') === 0) {
        return; // native window chrome — no-op in browser
      }
      Promise.resolve()
        .then(() => handle(msg.type as string, msg.payload))
        .then((payload) => emit(msg.requestId, true, payload, undefined))
        .catch((err: unknown) => {
          const record = (err ?? {}) as Record<string, unknown>;
          emit(msg.requestId, false, undefined, {
            code: readString(record, 'code') || 'host_error',
            message: readString(record, 'message') || String(err)
          });
        });
    },
    addEventListener(type: string, listener: (event: { data: unknown }) => void) {
      if (type === 'message') {
        listeners.add(listener);
      }
    },
    removeEventListener(type: string, listener: (event: { data: unknown }) => void) {
      if (type === 'message') {
        listeners.delete(listener);
      }
    }
  };

  // Preview mode: intercept platform API calls and serve mock data. Non-API requests (Vite assets,
  // HMR) pass through to the real fetch untouched.
  if (PREVIEW_MOCK) {
    const realFetch = globalThis.fetch.bind(globalThis);
    const mockedFetch = (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
      let pathname = '';
      try {
        pathname = new URL(String(input), location.origin).pathname;
      } catch {
        pathname = String(input);
      }
      return pathname.includes('/api/') ? devMockFetch(input, init) : realFetch(input, init);
    };
    globalThis.fetch = mockedFetch as typeof globalThis.fetch;
  }
}
