import { afterEach, describe, expect, it, mock } from 'bun:test';
import { loadOperatorSession, signInOperator, signOutOperator } from './authClient';
import type { HostBridgeMessageEvent } from './hostBridge';

describe('operator auth client', () => {
  afterEach(() => {
    delete window.chrome;
    localStorage.clear();
    sessionStorage.clear();
    mock.restore();
  });

  it('signs in through the native bridge without browser token persistence', async () => {
    const postMessage = installAuthBridge((message) => {
      expect(message).toMatchObject({
        type: 'auth:signIn',
        payload: {
          organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
          userName: 'cashier',
          password: 'password'
        }
      });

      return {
        staffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
        organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
        displayName: 'Cashier One',
        accessToken: 'access-token',
        accessTokenExpiresAtUtc: '2026-05-14T10:00:00Z',
        refreshTokenExpiresAtUtc: '2026-05-15T10:00:00Z',
        branchIds: ['acfc0212-967f-4d84-94be-9003387b09c2'],
        activeBranchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
        permissions: ['floor-map:view']
      };
    });

    const session = await signInOperator({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      userName: 'cashier',
      password: 'password'
    });

    expect(session.displayName).toBe('Cashier One');
    expect(postMessage).toHaveBeenCalledTimes(1);
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('loads and clears the native token session through host methods', async () => {
    const postMessage = installAuthBridge((message) => {
      if (message.type === 'auth:loadToken') {
        return null;
      }

      if (message.type === 'auth:signOut') {
        return { signedOut: true };
      }

      throw new Error(`Unexpected bridge call ${message.type}`);
    });

    await expect(loadOperatorSession()).resolves.toBeNull();
    await expect(signOutOperator()).resolves.toEqual({ signedOut: true });
    expect(postMessage).toHaveBeenCalledTimes(2);
  });
});

function installAuthBridge(respond: (message: { type: string; requestId: string; payload?: unknown }) => unknown) {
  const listeners = new Set<(event: HostBridgeMessageEvent) => void>();
  const postMessage = mock((message: unknown) => {
    const request = message as { type: string; requestId: string; payload?: unknown };
    const payload = respond(request);
    queueMicrotask(() => {
      for (const listener of listeners) {
        listener({
          data: {
            type: 'host:response',
            requestId: request.requestId,
            ok: true,
            payload
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

  return postMessage;
}
