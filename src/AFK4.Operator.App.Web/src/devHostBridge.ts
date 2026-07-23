// DEV-ONLY host-bridge stub. Emulates the native WebView2 host so the operator UI can run in a
// plain browser (or in a WPF preview host without a real backend) against mock/staging data. This
// file is imported only via a `import.meta.env.DEV` dynamic import in main.tsx, so it is
// tree-shaken out of the production `bun run build` output and never ships in the MSI.
//
// In the native WebView2 host the real bridge + injected __AFK4_OPERATOR_CONFIG__ already exist;
// installing this stub there would clobber the native bridge and point platformBaseUrl at the
// WebView origin (operator.afk4.local) instead of staging — so it stays inert when running inside
// the host.
//
// Auth is plain HTTP now (authClient.ts → StaffAuthApi), not a bridge round-trip — nothing in the
// app sends `auth:*` messages anymore, in either the browser or the WPF preview host. Preview
// sign-in is mocked by intercepting fetch (installMockFetch below), which serves `/api/auth/staff/*`
// from devMockFetch — see devMockBackend.ts.

import { devMockFetch } from './devMockBackend';

const ORG = '0169044b-2f74-46a7-8e52-7656a39a8f8c';
const BRANCH = 'f77b708c-1dc9-4cb3-9c19-21797f7035fc';

// UI-preview mode (default): serve a fake session + mock platform data so the console renders
// without a backend. Append `?live` to the dev URL to instead proxy sign-in to the staging API.
const PREVIEW_MOCK = !location.search.includes('live');

interface DevBridgeMessage {
  type?: string;
  requestId?: string;
  payload?: unknown;
}

interface DevWebView {
  postMessage(msg: DevBridgeMessage): void;
  addEventListener(type: string, listener: (event: { data: unknown }) => void): void;
  removeEventListener(type: string, listener: (event: { data: unknown }) => void): void;
}

export function installDevHostBridge(): void {
  const nativeWebview = (window as { chrome?: { webview?: DevWebView } }).chrome?.webview;
  if (nativeWebview) {
    // We only reach this module in a vite DEV run, so being inside a WebView2 host means the WPF
    // shell is pointing at the dev server (a preview shell) — never a packaged MSI. In preview mode
    // (default; opt out with `?live`) overlay mock platform data onto the native bridge so
    // window:*/connection:* still reach the real native bridge (titlebar buttons + connection
    // storage keep working).
    if (PREVIEW_MOCK) {
      installHostPreviewOverlay(nativeWebview);
    }
    return;
  }

  window.__AFK4_OPERATOR_CONFIG__ = createDevOperatorConfig();

  const listeners = new Set<(event: { data: unknown }) => void>();

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
      // Nothing else is sent over the bridge anymore (auth is HTTP; connection:* has no plain-
      // browser backing store here) — report unhandled rather than hanging the caller.
      emit(msg.requestId, false, undefined, { code: 'unhandled', message: 'dev bridge: unhandled ' + msg.type });
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

  if (PREVIEW_MOCK) {
    installMockFetch();
  }
}

export function createDevOperatorConfig() {
  return {
    runtime: 'browser-dev',
    shellMode: PREVIEW_MOCK ? 'vite-dev-preview' : 'vite-dev',
    platformBaseUrl: location.origin + '/',
    currencyCode: 'TJS',
    appVersion: 'dev',
    organizationId: ORG,
    branchId: BRANCH
  };
}

// Preview mode: intercept platform API calls and serve mock data. Non-API requests (Vite assets,
// HMR) pass through to the real fetch untouched.
function installMockFetch(): void {
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

// Inside the WebView2 host (dev preview shell): wrap the native bridge so platform /api/ calls serve
// fixtures (including auth, via installMockFetch/devMockFetch), while window:* (titlebar) and
// connection:* (storage) still reach the real native bridge.
function installHostPreviewOverlay(native: DevWebView): void {
  // The connection gate keys off org/branch in the injected config; seed them so preview lands on
  // the sign-in screen even when the host wasn't launched with the org/branch env vars.
  const cfg = window.__AFK4_OPERATOR_CONFIG__;
  if (cfg) {
    cfg.organizationId = cfg.organizationId || ORG;
    cfg.branchId = cfg.branchId || BRANCH;
  }

  const listeners = new Set<(event: { data: unknown }) => void>();
  const nativePost = native.postMessage.bind(native);
  native.addEventListener('message', (event) => {
    listeners.forEach((l) => {
      try {
        l(event);
      } catch {
        // Ignore listener faults; dev-only harness.
      }
    });
  });

  const overlay: DevWebView = {
    // Everything still goes to the real native bridge — auth no longer travels over it at all
    // (mocked at the fetch layer below), so there is nothing left to intercept here.
    postMessage(msg: DevBridgeMessage) {
      if (!msg || !msg.type) {
        return;
      }
      nativePost(msg);
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

  (window as unknown as { chrome: { webview: DevWebView } }).chrome.webview = overlay;
  installMockFetch();
}
