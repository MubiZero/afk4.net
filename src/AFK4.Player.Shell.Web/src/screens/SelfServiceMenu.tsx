import { useState } from 'react';
import type { ShellApi } from '../shellApi';
import { LoginScreen } from './LoginScreen';
import { ExtendScreen } from './ExtendScreen';
import { TopUpScreen } from './TopUpScreen';

export interface SelfServiceMenuProps {
  authenticated: boolean;
  onSignIn: (phoneNumber: string, password: string) => Promise<boolean>;
  api: ShellApi;
  sessionId: string | null;
  branchId: string;
  onReloadState: () => void;
}

type View = 'menu' | 'extend' | 'topup';

export function SelfServiceMenu({ authenticated, onSignIn, api, sessionId, branchId, onReloadState }: SelfServiceMenuProps) {
  const [view, setView] = useState<View>('menu');

  if (!authenticated) {
    return <LoginScreen onSubmit={onSignIn} />;
  }

  if (view === 'extend' && sessionId) {
    return <ExtendScreen api={api} branchId={branchId} sessionId={sessionId}
      onExtended={() => { setView('menu'); onReloadState(); }}
      onConflict={() => { setView('menu'); onReloadState(); }} />;
  }

  if (view === 'topup') {
    return <TopUpScreen api={api} amountMinorUnits={5000} />;
  }

  return (
    <nav aria-label="self-service">
      <button type="button" onClick={() => setView('extend')} disabled={!sessionId}>Продлить</button>
      <button type="button" onClick={() => setView('topup')}>Пополнить</button>
    </nav>
  );
}
