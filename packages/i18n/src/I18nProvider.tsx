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
const LOCALE_TAG: Record<Locale, string> = { ru: 'ru-RU', en: 'en-US' };

export function I18nProvider({ children, initialLocale = 'ru' }: { children: ReactNode; initialLocale?: Locale }) {
  const [locale, setLocale] = useState<Locale>(initialLocale);

  const t = useCallback((key: MessageKey): string => {
    const dict = messages[locale] as Record<string, string>;
    return dict[key] ?? key;
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
    [locale, t, formatNumber, formatCurrency, formatDate]
  );
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nContextValue {
  const ctx = useContext(I18nContext);
  if (ctx === null) throw new Error('useI18n must be used within I18nProvider');
  return ctx;
}
