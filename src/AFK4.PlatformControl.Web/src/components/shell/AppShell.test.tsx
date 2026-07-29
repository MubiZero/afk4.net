import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { buildPlatformNav } from '@/platform/nav';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { AppShell } from './AppShell';

function renderShell(onNavigate = mock()) {
  return render(
    <ThemeProvider><I18nProvider>
      <AppShell
        navGroups={buildPlatformNav({
          platformAdminId: 'admin-1', userName: 'owner', displayName: 'Platform Owner', roles: ['platform_admin'],
          permissions: ['platform.organizations.view', 'platform.billing.view'],
          accessToken: 'access', accessTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
          refreshToken: 'refresh', refreshTokenExpiresAtUtc: '2099-01-02T00:00:00Z'
        })}
        sidebarHeader={<div>Platform Control</div>}
        activePath="/admin"
        subtitle=""
        screenTitle="Обзор"
        userName="Platform Owner"
        roleLabel="Администратор"
        onNavigate={onNavigate}
        onSignOut={mock()}
      >
        <div>screen-body</div>
      </AppShell>
    </I18nProvider></ThemeProvider>
  );
}

describe('AppShell', () => {
  it('renders Platform Control navigation and body', () => {
    renderShell();
    expect(screen.getByText('Platform Control')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Организации' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Биллинг' })).toBeInTheDocument();
    expect(screen.getByText('screen-body')).toBeInTheDocument();
  });

  it('fires admin navigation on item click', () => {
    const onNavigate = mock();
    renderShell(onNavigate);
    fireEvent.click(screen.getByRole('button', { name: 'Обзор' }));
    expect(onNavigate).toHaveBeenCalledWith('/admin');
  });
});
