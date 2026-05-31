import { describe, expect, it, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { visibleNav } from '@/club/nav';
import { BranchSwitcher } from './BranchSwitcher';
import { AppShell } from './AppShell';

function renderShell(role: 'owner' | 'manager', onNavigate = mock()) {
  return render(
    <ThemeProvider><I18nProvider>
      <AppShell
        navGroups={visibleNav(role)}
        sidebarHeader={
          <BranchSwitcher orgName="Победа" branches={[{ branchId: 'b1', name: 'Центральный' }]}
            activeBranchId="b1" onSelect={mock()} />
        }
        activePath="/club"
        subtitle="Центральный"
        screenTitle="Обзор"
        userName="Алишер"
        roleLabel="Владелец"
        counts={{ venue: 2 }}
        onNavigate={onNavigate}
        onSignOut={mock()}
      >
        <div>screen-body</div>
      </AppShell>
    </I18nProvider></ThemeProvider>
  );
}

describe('AppShell', () => {
  it('renders branch + account groups and the body for an owner', () => {
    renderShell('owner');
    expect(screen.getByText('Филиал')).toBeInTheDocument();
    expect(screen.getByText('Аккаунт')).toBeInTheDocument();
    expect(screen.getByText('Настройки')).toBeInTheDocument();
    expect(screen.getByText('screen-body')).toBeInTheDocument();
  });

  it('hides owner-only items for a manager', () => {
    renderShell('manager');
    expect(screen.queryByText('Настройки')).not.toBeInTheDocument();
    expect(screen.queryByText('Биллинг')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Обзор' })).toBeInTheDocument();
  });

  it('fires navigation on item click', () => {
    const onNavigate = mock();
    renderShell('owner', onNavigate);
    fireEvent.click(screen.getByRole('button', { name: 'Обзор' }));
    expect(onNavigate).toHaveBeenCalledWith('/club');
  });
});
