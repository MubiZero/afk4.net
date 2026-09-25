import type { ReactNode } from 'react';
import { I18nCatalogProvider, createCatalogTranslator } from './core';
import { messages, type Locale } from './messages';

export { isLocale, useI18n } from './core';

/** Провайдер с полным каталогом — у Панели, приложения игрока и мастера. */
export function I18nProvider({ children, initialLocale }: { children: ReactNode; initialLocale?: Locale }) {
  return (
    <I18nCatalogProvider catalog={messages} initialLocale={initialLocale}>
      {children}
    </I18nCatalogProvider>
  );
}

// Standalone translator for a fixed locale — for non-React callers (pure state/data
// modules, unit tests) that need the same ICU + fallback behaviour as the provider's t.
export function createTranslator(locale: Locale) {
  return createCatalogTranslator(messages, locale);
}
