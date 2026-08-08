import { useEffect, useMemo, useRef, useState } from 'react';
import { AuthProvider, useAuth } from './useAuth';
import { ActiveSessionScreen } from './screens/ActiveSessionScreen';
import { LockedScreen } from './screens/LockedScreen';
import { SelfServiceMenu } from './screens/SelfServiceMenu';
import { PlayerShellStateNames } from './shellContracts';
import { useShellBridge } from './useShellBridge';
import { createShellApi } from './shellApi';
import { API_BASE } from './apiBase';

function ShellRouter() {
  const { state, launch, requestOperator } = useShellBridge();
  const { auth, signIn } = useAuth();
  const api = useMemo(() => createShellApi(API_BASE), []);

  // null means "not loaded yet, or failed to load" — every feature is treated as enabled in that
  // state. This list only drives what the menu shows: it's convenience, not a security boundary,
  // since the server rejects a disabled feature (403 feature_disabled) regardless of what the
  // client renders. Hiding a working section because of a network hiccup would be worse than
  // briefly showing one that then 403s, so we fail open here.
  const [features, setFeatures] = useState<string[] | null>(null);
  const featuresFetchedRef = useRef(false);
  useEffect(() => {
    if (!auth.authenticated) {
      featuresFetchedRef.current = false;
      setFeatures(null);
      return;
    }
    if (featuresFetchedRef.current) return;
    featuresFetchedRef.current = true;
    api.getFeatures().then(setFeatures).catch(() => { /* fail open, see comment above */ });
  }, [auth.authenticated, api]);

  const locked =
    state === null ||
    state.state === PlayerShellStateNames.Locked ||
    state.state === PlayerShellStateNames.Offline ||
    state.state === PlayerShellStateNames.Error;

  if (locked) {
    return <LockedScreen state={state} onRequestOperator={requestOperator} />;
  }

  return (
    <>
      <ActiveSessionScreen state={state} onLaunch={launch} onRequestOperator={requestOperator} />
      <SelfServiceMenu
        authenticated={auth.authenticated}
        onSignIn={(p, pw) => signIn(p, pw).then((s) => s.authenticated)}
        api={api}
        sessionId={state.sessionId}
        branchId={state.branchId}
        features={features}
        onReloadState={() => { /* state re-renders from bridge pushes; no-op reload */ }}
      />
    </>
  );
}

export function App() {
  return (
    <AuthProvider>
      <ShellRouter />
    </AuthProvider>
  );
}
