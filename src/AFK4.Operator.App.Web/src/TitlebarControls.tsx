import { Moon, Sun } from 'lucide-react';
import { useI18n, type Locale } from '@afk4/i18n';
import { useOperatorTheme } from './operatorTheme';

// Order the language toggle cycles through; the visible code shows the current locale.
const LOCALE_CYCLE: Locale[] = ['ru', 'en', 'tg'];
const LOCALE_CODE: Record<Locale, string> = { ru: 'RU', en: 'EN', tg: 'TJ' };

// Language + theme toggles shared by the auth titlebar and the signed-in console header, so both
// surfaces switch language and light/dark the same way.
export function TitlebarControls() {
  const { t, locale, setLocale } = useI18n();
  const { theme, toggleTheme } = useOperatorTheme();

  const cycleLanguage = () => {
    setLocale(LOCALE_CYCLE[(LOCALE_CYCLE.indexOf(locale) + 1) % LOCALE_CYCLE.length]);
  };
  const themeLabel = theme === 'dark' ? t('op.shell.themeToLight') : t('op.shell.themeToDark');

  return (
    <>
      <button
        type="button"
        className="titlebar-toolbtn"
        aria-label={t('op.shell.changeLanguage')}
        title={t('op.shell.changeLanguage')}
        onClick={cycleLanguage}
      >
        {LOCALE_CODE[locale]}
      </button>
      <button
        type="button"
        className="titlebar-toolbtn"
        aria-label={themeLabel}
        title={themeLabel}
        onClick={toggleTheme}
      >
        {theme === 'dark' ? <Moon size={15} aria-hidden /> : <Sun size={15} aria-hidden />}
      </button>
    </>
  );
}
