import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { AppShell } from './AppShell';

function renderShell(role: 'owner' | 'manager') {
  return render(
    <ThemeProvider><I18nProvider>
      <AppShell
        role={role}
        orgName="Победа"
        branches={[{ branchId: 'b1', name: 'Центральный' }]}
        activeBranchId="b1"
        activePath="/club"
        screenTitle="Обзор"
        userName="Алишер"
        roleLabel="Владелец"
        counts={{ venue: 2 }}
        onNavigate={vi.fn()}
        onSelectBranch={vi.fn()}
        onSignOut={vi.fn()}
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
    expect(screen.getByText('Обзор')).toBeInTheDocument();
  });

  it('fires navigation on item click', () => {
    const onNavigate = vi.fn();
    render(
      <ThemeProvider><I18nProvider>
        <AppShell role="owner" orgName="П" branches={[{ branchId: 'b1', name: 'Ц' }]} activeBranchId="b1"
          activePath="/club" screenTitle="Обзор" userName="A" roleLabel="Владелец"
          onNavigate={onNavigate} onSelectBranch={vi.fn()} onSignOut={vi.fn()}>
          <div />
        </AppShell>
      </I18nProvider></ThemeProvider>
    );
    fireEvent.click(screen.getByText('Обзор'));
    expect(onNavigate).toHaveBeenCalledWith('/club');
  });
});
