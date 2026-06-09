import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { ANONYMOUS, loadAuthState, onAuthChanged, signIn as apiSignIn, signOut as apiSignOut, type AuthSnapshot } from './shellAuth';

interface AuthContextValue {
  auth: AuthSnapshot;
  signIn: (phoneNumber: string, password: string) => Promise<AuthSnapshot>;
  signOut: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthSnapshot>(ANONYMOUS);

  useEffect(() => {
    let active = true;
    loadAuthState().then((s) => active && setAuth(s));
    const off = onAuthChanged(setAuth);
    return () => { active = false; off(); };
  }, []);

  const value = useMemo<AuthContextValue>(() => ({
    auth,
    signIn: async (phone, password) => { const s = await apiSignIn(phone, password); setAuth(s); return s; },
    signOut: async () => { const s = await apiSignOut(); setAuth(s); }
  }), [auth]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
