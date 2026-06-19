import { Search } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { BrandLogo } from './BrandLogo';
import { TitlebarControls } from './TitlebarControls';
import { WindowControls, handleWindowDragStart, handleWindowTitleDoubleClick } from './WindowChrome';

// Верхняя командная панель оболочки: бренд, поиск (палитра) и оконные контролы. Презентационная.
// Быстрые действия открываются палитрой (Ctrl+K / клик по поиску), статус смены живёт в подвале
// рейла рядом с аккаунтом.
export function ShellHeader({ onOpenPalette }: { onOpenPalette: () => void }) {
  const { t } = useI18n();
  return (
    <header className="top-command" onMouseDown={handleWindowDragStart} onDoubleClick={handleWindowTitleDoubleClick}>
      <div className="brand-block">
        <BrandLogo className="brand-logo" />
        <span>{t('op.auth.operator')}</span>
      </div>
      <button
        type="button"
        className="command-search"
        aria-label={t('op.shell.searchLabel')}
        onClick={onOpenPalette}
      >
        <Search size={16} />
        <span>{t('op.shell.searchPlaceholder')}</span>
      </button>
      <div className="top-right">
        <TitlebarControls />
        <span className="titlebar-separator" aria-hidden="true" />
        <WindowControls />
      </div>
    </header>
  );
}
