import { useCallback, useEffect, useRef, useState } from 'react';
import { PlayerApiClient } from './api/playerApi';
import type { PlayerSignInResponse } from './api/types';
import {
  readPlayerSession, writePlayerSession, clearPlayerSession,
  playerSessionFromSignInResponse, type PlayerSession
} from './auth/playerTokenStore';
import { resolvePlayerRoute, routePath, type PlayerRoute, type PlayerTab } from './routing';
import { AppShell } from './components/AppShell';
import { ToastProvider } from './components/ui/toast';
import { SignInScreen } from './screens/auth/SignInScreen';
import { DashboardScreen } from './screens/dashboard/DashboardScreen';
import { useBranding } from './branding/useBranding';

const API_BASE = import.meta.env.VITE_API_BASE ?? '';

function tabForRoute(route: PlayerRoute): PlayerTab {
  if (route.kind === 'receipt') return 'history';
  return route.kind;
}

export function App() {
  const [session, setSession] = useState<PlayerSession | null>(() => readPlayerSession());
  const [route, setRoute] = useState<PlayerRoute>(() =>
    resolvePlayerRoute(typeof window === 'undefined' ? '/' : window.location.pathname));

  // Stable client identity across auth changes: a silent token refresh updates the
  // client in place rather than rebuilding it (which would remount child screens and
  // restart their polling). The session is pushed into the client via updateSession.
  const apiRef = useRef<PlayerApiClient | null>(null);
  const onSessionChanged = useCallback((next: PlayerSession | null) => {
    setSession(next);
    apiRef.current?.updateSession(next);
    if (next) writePlayerSession(next); else clearPlayerSession();
  }, []);
  if (apiRef.current === null) {
    apiRef.current = new PlayerApiClient({ baseUrl: API_BASE, session, onSessionChanged });
  }
  const api = apiRef.current;

  const branding = useBranding({
    hostname: typeof window === 'undefined' ? '' : window.location.hostname,
    search: typeof window === 'undefined' ? '' : window.location.search,
    baseUrl: API_BASE,
    fallbackOrganizationId: import.meta.env.VITE_DEMO_ORG_ID ?? '',
  });

  // Keep route state in sync with browser/OS back-forward navigation.
  useEffect(() => {
    function onPopState() {
      setRoute(resolvePlayerRoute(window.location.pathname));
    }
    window.addEventListener('popstate', onPopState);
    return () => window.removeEventListener('popstate', onPopState);
  }, []);

  const navigate = useCallback((tab: PlayerTab) => {
    const next: PlayerRoute = { kind: tab };
    setRoute(next);
    if (typeof window !== 'undefined') window.history.pushState(null, '', routePath(next));
  }, []);

  const handleSignedIn = useCallback((response: PlayerSignInResponse) => {
    onSessionChanged(playerSessionFromSignInResponse(response));
  }, [onSessionChanged]);

  if (!session) {
    if (branding.status === 'loading') {
      return (
        <main className="flex min-h-dvh items-center justify-center" role="status" aria-label="Загрузка">
          <div className="h-10 w-10 animate-pulse rounded-full bg-[var(--color-surface)]" />
        </main>
      );
    }
    return (
      <SignInScreen
        organizationId={branding.organizationId}
        brandName={branding.brandName}
        signIn={(req) => api.signIn(req)}
        onSignedIn={handleSignedIn}
      />
    );
  }

  return (
    <ToastProvider>
      <AppShell active={tabForRoute(route)} onNavigate={navigate}>
        {route.kind === 'dashboard' && <DashboardScreen api={api} displayName={session.displayName} />}
        {route.kind !== 'dashboard' && (
          <section className="px-6 py-10 text-[var(--text-2)]">Скоро здесь появится этот раздел.</section>
        )}
      </AppShell>
    </ToastProvider>
  );
}
