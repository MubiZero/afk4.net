import { afterEach, beforeEach, describe, expect, it } from 'bun:test';
import { getOperatorConfig } from './operatorConfig';

describe('getOperatorConfig', () => {
  afterEach(() => {
    delete window.__AFK4_ORGANIZATION_ADMIN_CONFIG__;
  });

  it('uses localhost defaults outside the WebView2 host', () => {
    expect(getOperatorConfig()).toMatchObject({
      runtime: 'browser-dev',
      shellMode: 'vite-dev',
      platformBaseUrl: 'http://localhost:5074/',
      currencyCode: 'TJS'
    });
  });

  it('uses the config injected by the native host', () => {
    window.__AFK4_ORGANIZATION_ADMIN_CONFIG__ = {
      runtime: 'webview2',
      shellMode: 'vite-dist',
      platformBaseUrl: 'https://afk4.staging.mubi.dev/',
      currencyCode: 'USD'
    };

    expect(getOperatorConfig()).toMatchObject({
      runtime: 'webview2',
      shellMode: 'vite-dist',
      platformBaseUrl: 'https://afk4.staging.mubi.dev/',
      currencyCode: 'USD'
    });
  });

  it('uses the injected config when present, even in a plain browser', () => {
    (window as never as { __AFK4_ORGANIZATION_ADMIN_CONFIG__?: unknown }).__AFK4_ORGANIZATION_ADMIN_CONFIG__ =
      { runtime: 'browser', shellMode: 'web', platformBaseUrl: 'https://api.example/', currencyCode: 'TJS' };
    expect(getOperatorConfig().platformBaseUrl).toBe('https://api.example/');
  });

  describe('production browser build (no WebView2 injection)', () => {
    const env = import.meta.env as unknown as Record<string, unknown>;
    let originalProd: unknown;
    let originalBaseUrl: unknown;

    beforeEach(() => {
      originalProd = env.PROD;
      originalBaseUrl = env.VITE_PLATFORM_BASE_URL;
      env.PROD = true;
    });

    afterEach(() => {
      env.PROD = originalProd;
      env.VITE_PLATFORM_BASE_URL = originalBaseUrl;
    });

    it('reads platformBaseUrl from VITE_PLATFORM_BASE_URL when set', () => {
      env.VITE_PLATFORM_BASE_URL = 'https://api.example/';
      expect(getOperatorConfig()).toMatchObject({
        runtime: 'browser',
        shellMode: 'web',
        platformBaseUrl: 'https://api.example/'
      });
    });

    it('throws a configuration error instead of silently falling back to localhost', () => {
      delete env.VITE_PLATFORM_BASE_URL;
      expect(() => getOperatorConfig()).toThrow(/VITE_PLATFORM_BASE_URL/);
    });
  });
});
