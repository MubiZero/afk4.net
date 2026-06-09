import { postShellRequest } from './shellBridge';

export interface AuthSnapshot {
  authenticated: boolean;
  displayName: string | null;
  phoneVerified: boolean;
}

export const ANONYMOUS: AuthSnapshot = { authenticated: false, displayName: null, phoneVerified: false };

export function loadAuthState(): Promise<AuthSnapshot> {
  return postShellRequest<AuthSnapshot>('auth:loadState').catch(() => ANONYMOUS);
}

export function signIn(phoneNumber: string, password: string): Promise<AuthSnapshot> {
  return postShellRequest<AuthSnapshot>('auth:signIn', { phoneNumber, password });
}

export function signOut(): Promise<AuthSnapshot> {
  return postShellRequest<AuthSnapshot>('auth:signOut');
}

export function onAuthChanged(handler: (s: AuthSnapshot) => void): () => void {
  const webview = window.chrome?.webview;
  if (!webview?.addEventListener) return () => {};
  const listener = (event: { data: unknown }) => {
    const data = event.data as { type?: string; payload?: AuthSnapshot };
    if (data?.type === 'shell:authChanged' && data.payload) handler(data.payload);
  };
  webview.addEventListener('message', listener);
  return () => webview.removeEventListener?.('message', listener);
}
