import { afterEach, describe, expect, it, vi } from 'vitest';
import { postHostRequest, postHostWindowCommand, type HostBridgeMessageEvent } from './hostBridge';

describe('postHostWindowCommand', () => {
  afterEach(() => {
    delete window.chrome;
  });

  it('posts a narrow window command to the native host', () => {
    const postMessage = vi.fn();
    window.chrome = {
      webview: {
        postMessage
      }
    };

    postHostWindowCommand('drag');

    expect(postMessage).toHaveBeenCalledWith({ type: 'window:drag' });
  });

  it('does nothing outside WebView2', () => {
    expect(() => postHostWindowCommand('close')).not.toThrow();
  });
});

describe('postHostRequest', () => {
  afterEach(() => {
    delete window.chrome;
    vi.restoreAllMocks();
  });

  it('posts a request and resolves the matching host response', async () => {
    const listeners = new Set<(event: HostBridgeMessageEvent) => void>();
    const postMessage = vi.fn((message: unknown) => {
      const request = message as { requestId: string };
      queueMicrotask(() => {
        for (const listener of listeners) {
          listener({
            data: {
              type: 'host:response',
              requestId: request.requestId,
              ok: true,
              payload: { displayName: 'Cashier One' }
            }
          });
        }
      });
    });
    window.chrome = {
      webview: {
        postMessage,
        addEventListener: (_type, listener) => listeners.add(listener),
        removeEventListener: (_type, listener) => listeners.delete(listener)
      }
    };

    await expect(postHostRequest<{ displayName: string }>('auth:loadToken')).resolves.toEqual({
      displayName: 'Cashier One'
    });
    expect(postMessage).toHaveBeenCalledWith(expect.objectContaining({
      type: 'auth:loadToken',
      requestId: expect.any(String)
    }));
  });

  it('rejects failed host responses', async () => {
    const listeners = new Set<(event: HostBridgeMessageEvent) => void>();
    window.chrome = {
      webview: {
        postMessage: (message: unknown) => {
          const request = message as { requestId: string };
          queueMicrotask(() => {
            for (const listener of listeners) {
              listener({
                data: {
                  type: 'host:response',
                  requestId: request.requestId,
                  ok: false,
                  error: { code: 'auth_failed', message: 'Invalid credentials.' }
                }
              });
            }
          });
        },
        addEventListener: (_type, listener) => listeners.add(listener),
        removeEventListener: (_type, listener) => listeners.delete(listener)
      }
    };

    await expect(postHostRequest('auth:signIn')).rejects.toThrow('Invalid credentials.');
  });

  it('rejects when the native bridge is unavailable', async () => {
    await expect(postHostRequest('auth:loadToken')).rejects.toThrow('Native host bridge is unavailable.');
  });
});
