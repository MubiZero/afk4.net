import { useState } from 'react';
import type { ShellApi } from '../shellApi';
import { LoginScreen } from './LoginScreen';
import { ExtendScreen } from './ExtendScreen';
import { TopUpScreen } from './TopUpScreen';
import { ShopScreen } from './ShopScreen';
import { LoyaltyScreen } from './LoyaltyScreen';
import { NewsScreen } from './NewsScreen';

export interface SelfServiceMenuProps {
  authenticated: boolean;
  onSignIn: (phoneNumber: string, password: string) => Promise<boolean>;
  api: ShellApi;
  sessionId: string | null;
  branchId: string;
  // null = list unavailable (not loaded yet, or failed) — every feature is treated as enabled;
  // see the fail-open comment in App.tsx.
  features: string[] | null;
  onReloadState: () => void;
}

type View = 'menu' | 'extend' | 'topup' | 'shop' | 'loyalty' | 'news';

export function SelfServiceMenu({ authenticated, onSignIn, api, sessionId, branchId, features, onReloadState }: SelfServiceMenuProps) {
  const [view, setView] = useState<View>('menu');

  if (!authenticated) {
    return <LoginScreen onSubmit={onSignIn} />;
  }

  const hasFeature = (key: string) => features === null || features.includes(key);

  if (view === 'extend' && sessionId) {
    return <ExtendScreen api={api} branchId={branchId} sessionId={sessionId}
      onExtended={() => { setView('menu'); onReloadState(); }}
      onConflict={() => { setView('menu'); onReloadState(); }} />;
  }

  // The `hasFeature` guard here — not just on the menu button below — closes the direct entrance
  // too: a disabled feature's screen never renders regardless of how `view` got set (e.g. Shop's
  // "insufficient funds" redirect into top-up). When the guard fails, none of the branches below
  // match either, so the component falls through to the menu.
  if (view === 'topup' && hasFeature('online_topup')) {
    return <TopUpScreen api={api} amountMinorUnits={5000} />;
  }

  if (view === 'shop' && hasFeature('player_shop')) {
    return <ShopScreen api={api}
      onNeedTopUp={() => { if (hasFeature('online_topup')) setView('topup'); }}
      onDone={() => { setView('menu'); onReloadState(); }} />;
  }

  if (view === 'loyalty' && hasFeature('loyalty')) {
    return <LoyaltyScreen api={api} onDone={() => { setView('menu'); onReloadState(); }} />;
  }

  if (view === 'news') {
    return <NewsScreen api={api} onDone={() => { setView('menu'); onReloadState(); }} />;
  }

  return (
    <nav aria-label="self-service">
      <button type="button" onClick={() => setView('extend')} disabled={!sessionId}>Продлить</button>
      {hasFeature('online_topup') && <button type="button" onClick={() => setView('topup')}>Пополнить</button>}
      {hasFeature('player_shop') && <button type="button" onClick={() => setView('shop')} disabled={!sessionId}>Магазин</button>}
      {hasFeature('loyalty') && <button type="button" onClick={() => setView('loyalty')}>Кэшбэк</button>}
      <button type="button" onClick={() => setView('news')}>Новости</button>
    </nav>
  );
}
