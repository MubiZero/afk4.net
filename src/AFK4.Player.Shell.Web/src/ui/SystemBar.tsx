import { ShellBridgeRequestTypeNames, type ShellSystemStateDto } from '@afk4/contracts';
import { useI18n, type Locale } from '@afk4/i18n';
import { Wifi, WifiOff } from 'lucide-react';
import { requestHost } from '../host/shellHost';
import { SystemControls } from './SystemControls';
import { useClock } from './useSecondTick';

/**
 * Системная строка: без проводника её нет больше нигде (спека, §3). Язык, связь и часы клуба.
 * Названия языков — на самих языках, как их ищет человек, поэтому они не в каталоге строк.
 */
const LANGUAGES: { locale: Locale; label: string }[] = [
  { locale: 'ru', label: 'Рус' },
  { locale: 'tg', label: 'Тоҷ' },
  { locale: 'en', label: 'Eng' }
];

interface SystemBarProps {
  online: boolean;
  /** Звук, микрофон, раскладка — от хоста; null — кнопок нет. */
  system?: ShellSystemStateDto | null;
  /** Человек выбрал язык сам — с этой минуты язык филиала его не перебивает. */
  onLocaleChosen?: () => void;
}

export function SystemBar({ online, system = null, onLocaleChosen }: SystemBarProps) {
  const { t, locale, setLocale } = useI18n();
  const now = useClock('minute');

  const choose = (next: Locale) => {
    onLocaleChosen?.();
    setLocale(next);
    // Хост помнит выбор до выхода игрока; без хоста язык просто меняется на странице.
    requestHost(ShellBridgeRequestTypeNames.UiSetLocale, { locale: next }).catch(() => {});
  };

  return (
    <footer className="system-bar">
      <div className="system-bar__languages" role="group" aria-label={t('playerShell.system.language')}>
        {LANGUAGES.map((language) => (
          <button
            key={language.locale}
            type="button"
            className="system-bar__language"
            aria-pressed={language.locale === locale}
            onClick={() => choose(language.locale)}
          >
            {language.label}
          </button>
        ))}
      </div>
      <span className="system-bar__grow" />
      <SystemControls system={system} />
      <span className={online ? 'system-bar__network' : 'system-bar__network system-bar__network--down'}>
        {online ? <Wifi aria-hidden="true" /> : <WifiOff aria-hidden="true" />}
        {online ? t('playerShell.system.online') : t('playerShell.system.offline')}
      </span>
      <span className="system-bar__clock mono">
        {new Date(now).toLocaleTimeString(locale === 'en' ? 'en-GB' : 'ru-RU', { hour: '2-digit', minute: '2-digit' })}
      </span>
    </footer>
  );
}
