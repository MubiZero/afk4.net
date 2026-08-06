import type { ReactNode } from 'react';
import { useI18n } from '@/i18n/I18nProvider';
import { AccountMenu } from './AccountMenu';
import { BrandLogo } from './BrandLogo';
import type { NavItem } from './navModel';

export interface AppShellProps {
  navItems: NavItem[];
  activePath: string;
  userName: string;
  roleLabel: string;
  permissions: string[];
  search?: ReactNode;
  onNavigate: (path: string) => void;
  onSignOut: () => void;
  children: ReactNode;
}

// Оболочка панели: командная панель сверху, иконочный рейл слева, рабочая область справа —
// та же геометрия, что у Organization Admin, минус оконные контролы десктопного хоста.
export function AppShell({ navItems, activePath, userName, roleLabel, permissions, search, onNavigate, onSignOut, children }: AppShellProps) {
  const { t } = useI18n();
  return (
    <div className="pc-shell">
      <header className="top-command">
        <div className="top-command-left">
          <div className="brand-block">
            <BrandLogo className="brand-logo" />
            <span>{t('shell.brand.section')}</span>
          </div>
        </div>
        {search ?? <span />}
        <div className="top-right" />
      </header>

      <nav className="workspace-rail" aria-label={t('shell.nav.label')}>
        {navItems.map(item => {
          const Icon = item.icon;
          const active = item.path === activePath;
          return (
            <button
              key={item.key}
              type="button"
              className={active ? 'active' : undefined}
              aria-current={active ? 'page' : undefined}
              onClick={() => onNavigate(item.path)}
            >
              <Icon size={18} aria-hidden="true" />
              <span>{t(item.labelKey)}</span>
            </button>
          );
        })}
        <AccountMenu displayName={userName} roleLabel={roleLabel} permissions={permissions} onSignOut={onSignOut} />
      </nav>

      <main className="pc-workspace">{children}</main>
    </div>
  );
}
