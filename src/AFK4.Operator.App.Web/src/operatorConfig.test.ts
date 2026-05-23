import { afterEach, describe, expect, it } from 'vitest';
import { getOperatorConfig } from './operatorConfig';

describe('getOperatorConfig', () => {
  afterEach(() => {
    delete window.__AFK4_OPERATOR_CONFIG__;
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
    window.__AFK4_OPERATOR_CONFIG__ = {
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
});
