import { Search } from 'lucide-react';
import type { ComponentProps } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';
import { BrandLogo } from './BrandLogo';
import { QuickActionsMenu } from './QuickActionsMenu';
import { TitlebarControls } from './TitlebarControls';
import { WindowControls, handleWindowDragStart, handleWindowTitleDoubleClick } from './WindowChrome';

// Верхняя командная панель оболочки: бренд, быстрые действия, поиск (палитра), смена и оконные
// контролы. Презентационная — действия и текст смены приходят пропсами.
export function ShellHeader({
  session,
  shiftText,
  onOpenPalette,
  onQuickAction
}: {
  session: OperatorAuthSession | null;
  shiftText: string;
  onOpenPalette: () => void;
  onQuickAction: ComponentProps<typeof QuickActionsMenu>['onSelect'];
}) {
  const { t } = useI18n();
  return (
    <header className="top-command" onMouseDown={handleWindowDragStart} onDoubleClick={handleWindowTitleDoubleClick}>
      <div className="brand-block">
        <BrandLogo className="brand-logo" />
        <span>{t('op.auth.operator')}</span>
      </div>
      <QuickActionsMenu session={session} onSelect={onQuickAction} />
      <button
        type="button"
        className="command-search"
        aria-label={t('op.shell.searchLabel')}
        onClick={onOpenPalette}
      >
        <Search size={16} />
        <span>{t('op.shell.searchPlaceholder')}</span>
      </button>
      <div className="top-status">
        <span>{shiftText}</span>
      </div>
      <TitlebarControls />
      <span className="titlebar-separator" aria-hidden="true" />
      <WindowControls />
    </header>
  );
}
