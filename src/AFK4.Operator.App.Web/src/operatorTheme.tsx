import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { postHostWindowTheme } from './hostBridge';

type Theme = 'light' | 'dark';

const STORAGE_KEY = 'afk4.operator.theme';

interface ThemeContextValue {
  theme: Theme;
  toggleTheme: () => void;
}

// Default (no provider, e.g. in unit tests) is dark + no-op, matching the shipping baseline.
const ThemeContext = createContext<ThemeContextValue>({ theme: 'dark', toggleTheme: () => {} });

function readInitialTheme(): Theme {
  try {
    const stored = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null;
    if (stored === 'light' || stored === 'dark') return stored;
  } catch {
    /* localStorage unavailable — fall through to the default */
  }
  // Cyber-club context: dark is the baseline so a night shift isn't hit with a white screen.
  // Only an explicit user choice (persisted below) overrides it.
  return 'dark';
}

export function OperatorThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<Theme>(readInitialTheme);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    postHostWindowTheme(theme);
  }, [theme]);

  const toggleTheme = useCallback(() => {
    setTheme((current) => {
      const next = current === 'dark' ? 'light' : 'dark';
      try {
        localStorage.setItem(STORAGE_KEY, next);
      } catch {
        /* persistence is best-effort */
      }
      return next;
    });
  }, []);

  return <ThemeContext.Provider value={{ theme, toggleTheme }}>{children}</ThemeContext.Provider>;
}

export function useOperatorTheme(): ThemeContextValue {
  return useContext(ThemeContext);
}
