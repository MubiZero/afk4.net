// AUTO-GENERATED from locales/*.json by packages/i18n/scripts/generate-messages.ts.
// Do not edit by hand — edit locales/{ru,en,tg}.json then run `bun run gen` in packages/i18n.
import { ru } from './messages.ru';
import { en } from './messages.en';
import { tg } from './messages.tg';

export type Locale = 'ru' | 'en' | 'tg';

export const messages = {
  ru,
  en,
  tg,
} as const;

export type MessageKey = keyof (typeof messages)['ru'];
