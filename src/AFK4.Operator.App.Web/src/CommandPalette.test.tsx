import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { CommandPalette } from './CommandPalette';
import type { OperatorAuthSession } from './authClient';

const makeSession = (permissions: string[]) => ({ permissions } as unknown as OperatorAuthSession);

// Менеджер: видит зал, брони, клиентов + управление (открывает management).
const managerPerms = ['floor_map.view', 'reservations.view', 'players.view', 'identity.branch_staff.manage'];
// Кассир: только создание продаж — открывает pos/shop_orders, но НЕ management.
const cashierPerms = ['pos.sales.create'];

function renderPalette(perms: string[], handlers: { onNavigate?: (id: string) => void; onClose?: () => void } = {}) {
  const onNavigate = handlers.onNavigate ?? mock(() => {});
  const onClose = handlers.onClose ?? mock(() => {});
  render(
    <I18nProvider>
      <CommandPalette session={makeSession(perms)} onNavigate={onNavigate as never} onClose={onClose} />
    </I18nProvider>
  );
  return { onNavigate, onClose };
}

describe('CommandPalette', () => {
  afterEach(() => {
    cleanup();
  });

  it('renders the autofocused search field, nav heading and several option rows', () => {
    renderPalette(managerPerms);
    const input = screen.getByLabelText('Командная палитра');
    expect(input).toBeDefined();
    expect(screen.getByText('Переход к экрану')).toBeDefined();
    const options = screen.getAllByRole('option');
    expect(options.length).toBeGreaterThan(1);
  });

  it('filters the nav list by the localized label as the user types', () => {
    renderPalette(managerPerms);
    // До ввода видны и "Карта", и "Управление".
    expect(screen.getByText('Карта')).toBeDefined();
    expect(screen.getByText('Управление')).toBeDefined();
    fireEvent.change(screen.getByLabelText('Командная палитра'), { target: { value: 'Карта' } });
    expect(screen.getByText('Карта')).toBeDefined();
    expect(screen.queryByText('Управление')).toBeNull();
  });

  it('Enter on the active row navigates then closes', () => {
    const onNavigate = mock(() => {});
    const onClose = mock(() => {});
    renderPalette(managerPerms, { onNavigate, onClose });
    const input = screen.getByLabelText('Командная палитра');
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(onNavigate).toHaveBeenCalledTimes(1);
    const navigatedId = (onNavigate.mock.calls[0] as unknown[])[0];
    expect(typeof navigatedId).toBe('string');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('Esc closes the palette', () => {
    const onClose = mock(() => {});
    renderPalette(managerPerms, { onClose });
    fireEvent.keyDown(screen.getByLabelText('Командная палитра'), { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('shows a non-interactive "coming soon" entity block', () => {
    renderPalette(managerPerms);
    const soon = screen.getByText('Поиск клиентов, ПК и чеков появится позже');
    expect(soon).toBeDefined();
    expect(soon.closest('button')).toBeNull();
  });

  it('hides screens the session cannot open (cashier has no management screen)', () => {
    renderPalette(cashierPerms);
    expect(screen.getByText('Касса')).toBeDefined();
    expect(screen.queryByText('Управление')).toBeNull();
  });
});
