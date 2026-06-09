import { useMemo } from 'react';
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
