import { type ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { WindowControls, WindowResizeHandles, handleWindowDragStart, handleWindowTitleDoubleClick } from './WindowChrome';
import { TitlebarControls } from './TitlebarControls';
import { BrandLogo } from './BrandLogo';

// Shared chrome for the signed-out screens (sign-in, forgot password). Keeps the native titlebar —
// brand, language + theme toggles, window controls, drag region and resize handles — identical
// across the auth flow, so the window stays movable/resizable everywhere (the forgot screen used to
// ship without it).
export function AuthFrame({ children }: { children: ReactNode }) {
  const { t } = useI18n();

  return (
    <div className="operator-shell auth-shell">
      <WindowResizeHandles />
      <header className="top-command auth-top-command" onMouseDown={handleWindowDragStart} onDoubleClick={handleWindowTitleDoubleClick}>
        <div className="brand-block">
          <BrandLogo className="brand-logo" />
          <span>{t('op.auth.operator')}</span>
        </div>
        <div className="auth-titlebar-controls">
          <TitlebarControls />
          <WindowControls />
        </div>
      </header>
      <main className="auth-workspace">{children}</main>
    </div>
  );
}
