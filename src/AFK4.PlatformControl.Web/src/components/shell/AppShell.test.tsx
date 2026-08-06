import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { buildPlatformNav } from '@/platform/nav';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { AppShell } from './AppShell';

function renderShell(onNavigate = mock(), onSignOut = mock()) {
  return render(
    <ThemeProvider><I18nProvider>
      <AppShell
        navItems={buildPlatformNav({
          platformAdminId: 'admin-1', userName: 'owner', displayName: 'Platform Owner', roles: ['platform_admin'],
          permissions: ['platform.organizations.view', 'platform.billing.view'],
          accessToken: 'access', accessTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
          refreshToken: 'refresh', refreshTokenExpiresAtUtc: '2099-01-02T00:00:00Z'
        })}
        activePath="/admin"
        userName="Platform Owner"
        roleLabel="Администратор"
        permissions={['platform.organizations.view']}
        onNavigate={onNavigate}
        onSignOut={onSignOut}
      >
        <div>screen-body</div>
      </AppShell>
    </I18nProvider></ThemeProvider>
  );
}

describe('AppShell', () => {
  it('renders Platform Control navigation and body', () => {
    renderShell();
    expect(screen.getByRole('button', { name: 'Клубы' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Деньги' })).toBeInTheDocument();
    expect(screen.getByText('screen-body')).toBeInTheDocument();
  });

  it('fires admin navigation on item click', () => {
    const onNavigate = mock();
    renderShell(onNavigate);
    fireEvent.click(screen.getByRole('button', { name: 'Клубы' }));
    expect(onNavigate).toHaveBeenCalledWith('/admin');
  });

  // Учётная запись и выход живут в подвале рейла — отдельного экрана «Профиль» больше нет,
  // поэтому выйти из панели можно только отсюда.
  it('signs out from the rail account menu', () => {
    const onSignOut = mock();
    renderShell(mock(), onSignOut);
    fireEvent.click(screen.getByRole('button', { name: 'Учётная запись' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Выйти' }));
    expect(onSignOut).toHaveBeenCalled();
  });
});
