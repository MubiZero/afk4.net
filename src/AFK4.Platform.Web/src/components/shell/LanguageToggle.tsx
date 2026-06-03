import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import type { Locale } from '@/i18n/messages';

const ORDER: Locale[] = ['ru', 'en', 'tg'];
const LABEL: Record<Locale, string> = { ru: 'RU', en: 'EN', tg: 'TG' };

/** Compact language switcher: cycles ru → en → tg → ru. The chosen locale is
 * persisted by the i18n provider (localStorage), so it survives reload. */
export function LanguageToggle() {
  const { locale, setLocale, t } = useI18n();
  const next = ORDER[(ORDER.indexOf(locale) + 1) % ORDER.length];
  return (
    <Button variant="ghost" size="sm" aria-label={t('shell.language.toggle')} onClick={() => setLocale(next)}>
      {LABEL[locale]}
    </Button>
  );
}
