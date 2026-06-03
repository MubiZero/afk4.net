import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { formatNumber as fmtNumber, formatCurrency as fmtCurrency, formatDateParts } from '@afk4/formatting';
import { messages, type Locale, type MessageKey } from './messages';

interface I18nContextValue {
  locale: Locale;
  setLocale: (l: Locale) => void;
  t: (key: MessageKey) => string;
  formatNumber: (n: number) => string;
  formatCurrency: (amount: number, currencyCode: string) => string;
  formatDate: (iso: string) => string;
}
const I18nContext = createContext<I18nContextValue | null>(null);
const LOCALE_TAG: Record<Locale, string> = { ru: 'ru-RU', en: 'en-US', tg: 'tg-TJ' };
// Locale fallback (decision P6): tg → ru → key, en → key, ru → key. ru is this
// market's lingua franca, so an untranslated tg string degrades to ru, never EN.
const LOCALE_FALLBACK: Record<Locale, readonly Locale[]> = { ru: [], en: [], tg: ['ru'] };
const DEFAULT_LOCALE: Locale = 'ru';
const STORAGE_KEY = 'afk4.locale';

export function isLocale(value: unknown): value is Locale {
  return value === 'ru' || value === 'en' || value === 'tg';
}

function readStoredLocale(): Locale {
  try {
    const stored = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null;
    return isLocale(stored) ? stored : DEFAULT_LOCALE; // clamp unknown/stale values to ru
  } catch {
    return DEFAULT_LOCALE;
  }
}

function writeStoredLocale(locale: Locale): void {
  try {
    if (typeof localStorage !== 'undefined') localStorage.setItem(STORAGE_KEY, locale);
  } catch {
    /* persistence is best-effort; ignore quota/availability errors */
  }
}

export function I18nProvider({ children, initialLocale }: { children: ReactNode; initialLocale?: Locale }) {
  // Explicit prop wins (tests, server-seeded value); otherwise the persisted choice, else ru.
  const [locale, setLocaleState] = useState<Locale>(() => initialLocale ?? readStoredLocale());

  const setLocale = useCallback((l: Locale) => {
    setLocaleState(l);
    writeStoredLocale(l);
  }, []);

  const t = useCallback((key: MessageKey): string => {
    const direct = (messages[locale] as Record<string, string>)[key];
    if (direct !== undefined) return direct;
    for (const fb of LOCALE_FALLBACK[locale]) {
      const value = (messages[fb] as Record<string, string>)[key];
      if (value !== undefined) return value;
    }
    return key;
  }, [locale]);

  const formatNumber = useCallback((n: number) => fmtNumber(n, LOCALE_TAG[locale]), [locale]);
  const formatCurrency = useCallback(
    (amount: number, currencyCode: string) => fmtCurrency(amount, currencyCode, LOCALE_TAG[locale]),
    [locale]
  );
  const formatDate = useCallback(
    (iso: string) => formatDateParts(iso, LOCALE_TAG[locale], { dateStyle: 'medium', timeStyle: 'short' }),
    [locale]
  );

  const value = useMemo(
    () => ({ locale, setLocale, t, formatNumber, formatCurrency, formatDate }),
    [locale, setLocale, t, formatNumber, formatCurrency, formatDate]
  );
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nContextValue {
  const ctx = useContext(I18nContext);
  if (ctx === null) throw new Error('useI18n must be used within I18nProvider');
  return ctx;
}
