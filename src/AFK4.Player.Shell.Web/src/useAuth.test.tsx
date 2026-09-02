import { render, screen, waitFor } from '@testing-library/react';
import { act } from 'react';
import { describe, expect, it, beforeEach } from 'bun:test';
import { AuthProvider, useAuth } from './useAuth';
import type { AuthSnapshot } from './shellAuth';

const ANON: AuthSnapshot = { authenticated: false, displayName: null, phoneVerified: false };
const ALEX: AuthSnapshot = { authenticated: true, displayName: 'Alex', phoneVerified: true };

function installBridge(responder: (msg: any) => any) {
  const listeners: Array<(e: { data: unknown }) => void> = [];
  (window as any).chrome = {
    webview: {
      postMessage(msg: any) {
        const payload = responder(msg);
        queueMicrotask(() => {
          listeners.forEach((l) => {
            l({ data: { type: 'host:response', requestId: msg.requestId, ok: true, payload } });
          });
        });
      },
      addEventListener: (_t: string, l: any) => listeners.push(l),
      removeEventListener: () => {}
    }
  };
}

function Probe() {
  const { auth } = useAuth();
  return <div>{auth.authenticated ? `hi ${auth.displayName}` : 'anon'}</div>;
}

describe('useAuth', () => {
  beforeEach(() => { delete (window as any).chrome; });

  it('loads anonymous auth state on mount', async () => {
    installBridge(() => ANON);
    render(<AuthProvider><Probe /></AuthProvider>);
    await waitFor(() => expect(screen.getByText('anon')).toBeInTheDocument());
  });

  it('reflects a successful sign-in', async () => {
    // Bridge is stateful: signIn updates the shared state so any concurrent
    // loadAuthState response also returns the authenticated snapshot.
    let state = ANON;
    installBridge((msg) => {
      if (msg.type === 'auth:signIn') { state = ALEX; }
      return state;
    });

    // Expose signIn via a ref so the test can await it directly inside act(),
    // giving React time to flush the resulting state update before we assert.
    const signInRef = { current: (_p: string, _pw: string): Promise<AuthSnapshot> => Promise.resolve(ANON) };
    function SignInProbe() {
      const { auth, signIn } = useAuth();
      signInRef.current = signIn;
      return (
        <>
          <button>in</button>
          <span>{auth.authenticated ? `hi ${auth.displayName}` : 'anon'}</span>
        </>
      );
    }
    render(<AuthProvider><SignInProbe /></AuthProvider>);
    // Wait for mount so signInRef.current is bound to the real signIn from context.
    await waitFor(() => screen.getByText('in'));
    await act(async () => { await signInRef.current('+992900000000', 'pw'); });
    expect(screen.getByText('hi Alex')).toBeInTheDocument();
  });
});
