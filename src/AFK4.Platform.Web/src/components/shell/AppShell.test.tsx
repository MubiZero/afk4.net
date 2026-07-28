import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { platformNav } from '@/platform/nav';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { AppShell } from './AppShell';

function renderShell(onNavigate = mock()) {
  return render(
    <ThemeProvider><I18nProvider>
      <AppShell
        navGroups={platformNav}
        sidebarHeader={<div>AFK4.NET Control Plane</div>}
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
  it('renders Control Plane navigation and body', () => {
    renderShell();
    expect(screen.getByText('AFK4.NET Control Plane')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Тенанты' })).toBeInTheDocument();
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
