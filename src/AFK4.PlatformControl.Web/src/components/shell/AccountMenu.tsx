import { useEffect, useRef, useState } from 'react';
import { LogOut, Moon, Sun, Languages } from 'lucide-react';
import { useI18n } from '@/i18n/I18nProvider';
import type { Locale } from '@/i18n/messages';
import { useTheme } from '@/theme/ThemeProvider';

const LOCALE_ORDER: Locale[] = ['ru', 'en', 'tg'];
const LOCALE_LABEL: Record<Locale, string> = { ru: 'Русский', en: 'English', tg: 'Тоҷикӣ' };

// Аккаунт живёт в подвале рейла, как в Organization Admin: имя, роль, тема, язык и выход.
// Отдельного экрана «Профиль» нет — там нечего было делать, кроме как смотреть на свои права.
export function AccountMenu({ displayName, roleLabel, permissions, onSignOut }: {
  displayName: string;
  roleLabel: string;
  permissions: string[];
  onSignOut: () => void;
}) {
  const { t, locale, setLocale } = useI18n();
  const { theme, toggle } = useTheme();
  const [open, setOpen] = useState(false);
  const container = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      if (container.current !== null && !container.current.contains(event.target as Node)) setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const nextLocale = LOCALE_ORDER[(LOCALE_ORDER.indexOf(locale) + 1) % LOCALE_ORDER.length];

  return (
    <div className="rail-account" ref={container}>
      <button
        type="button"
        className="rail-account-trigger"
        aria-expanded={open}
        aria-haspopup="menu"
        aria-label={t('shell.account.open')}
        onClick={() => setOpen(value => !value)}
      >
        <span className="rail-account-avatar" aria-hidden="true">{displayName.slice(0, 1).toUpperCase()}</span>
      </button>

      {open ? (
        <div className="rail-account-menu pc-account-menu" role="menu">
          <div className="rail-account-head">
            <span className="rail-account-avatar lg" aria-hidden="true">{displayName.slice(0, 1).toUpperCase()}</span>
            <div className="pc-account-id">
              <strong>{displayName}</strong>
              <span>{roleLabel}</span>
            </div>
          </div>

          <button type="button" role="menuitem" className="rail-account-item" onClick={toggle}>
            {theme === 'dark' ? <Sun size={15} aria-hidden="true" /> : <Moon size={15} aria-hidden="true" />}
            {t(theme === 'dark' ? 'shell.theme.light' : 'shell.theme.dark')}
          </button>

          <button type="button" role="menuitem" className="rail-account-item" onClick={() => setLocale(nextLocale)}>
            <Languages size={15} aria-hidden="true" />
            {LOCALE_LABEL[nextLocale]}
          </button>

          <div className="mgmt-menu-sep" />

          {/* Права — справка, а не экран: платформенный админ должен видеть, что ему доступно,
              когда кнопка не появилась, но ради этого не нужен отдельный раздел навигации. */}
          <details className="pc-account-perms">
            <summary>{t('profile.permissions.title')}</summary>
            <ul>{permissions.map(permission => <li key={permission}>{permission}</li>)}</ul>
          </details>

          <button type="button" role="menuitem" className="rail-account-item danger" onClick={onSignOut}>
            <LogOut size={15} aria-hidden="true" />
            {t('auth.action.signOut')}
          </button>
        </div>
      ) : null}
    </div>
  );
}
