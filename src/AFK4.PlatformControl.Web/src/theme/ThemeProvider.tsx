import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { applyThemeClass, resolveInitialTheme, THEME_STORAGE_KEY, type Theme } from './theme';

interface ThemeContextValue { theme: Theme; setTheme: (t: Theme) => void; toggle: () => void; }
const ThemeContext = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(() =>
    resolveInitialTheme(typeof localStorage === 'undefined' ? null : localStorage.getItem(THEME_STORAGE_KEY))
  );

  useEffect(() => { applyThemeClass(theme); }, [theme]);

  const setTheme = useCallback((t: Theme) => {
    setThemeState(t);
    try { localStorage.setItem(THEME_STORAGE_KEY, t); } catch { /* ignore */ }
  }, []);

  const toggle = useCallback(() => { setTheme(theme === 'dark' ? 'light' : 'dark'); }, [theme, setTheme]);

  const value = useMemo(() => ({ theme, setTheme, toggle }), [theme, setTheme, toggle]);
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (ctx === null) throw new Error('useTheme must be used within ThemeProvider');
  return ctx;
}
