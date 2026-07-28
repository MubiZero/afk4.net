import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { QuickActionsMenu } from './QuickActionsMenu';
import type { QuickAction } from './operatorCommands';
import type { OperatorAuthSession } from './authClient';

const sessionWith = (perms: string[]) => ({ permissions: perms } as unknown as OperatorAuthSession);

const renderMenu = (perms: string[], onSelect = mock((_action: QuickAction) => {})) => {
  render(
    <I18nProvider>
      <QuickActionsMenu session={sessionWith(perms)} onSelect={onSelect} />
    </I18nProvider>
  );
  return onSelect;
};

const managerPerms = [
  'organization.sessions.start',
  'organization.pos.sales.create',
  'organization.players.create',
  'organization.reservations.manage',
  'organization.billing.wallet.top_up',
  'organization.shifts.cash.manage',
  'organization.inventory.stock.manage'
];

describe('QuickActionsMenu', () => {
  afterEach(() => {
    cleanup();
  });

  it('shows both groups and many items for a manager session', () => {
    renderMenu(managerPerms);
    fireEvent.click(screen.getByLabelText('Быстрое меню'));
    expect(screen.getByText('Сессии и клиенты')).toBeInTheDocument();
    expect(screen.getByText('Деньги и склад')).toBeInTheDocument();
    expect(screen.getAllByRole('menuitem').length).toBeGreaterThanOrEqual(6);
  });

  it('shows only the permitted action for a cashier session', () => {
    renderMenu(['organization.pos.sales.create']);
    fireEvent.click(screen.getByLabelText('Быстрое меню'));
    const items = screen.getAllByRole('menuitem');
    expect(items).toHaveLength(1);
    expect(screen.getByText('Продажа товара')).toBeInTheDocument();
    expect(screen.queryByText('Внесение на склад')).toBeNull();
  });

  it('calls onSelect with the chosen action', () => {
    const onSelect = renderMenu(managerPerms);
    fireEvent.click(screen.getByLabelText('Быстрое меню'));
    fireEvent.click(screen.getByText('Продажа товара'));
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect.mock.calls[0]?.[0]?.id).toBe('sell_product');
  });

  it('closes the menu on Escape', () => {
    renderMenu(managerPerms);
    fireEvent.click(screen.getByLabelText('Быстрое меню'));
    expect(screen.getByRole('menu')).toBeInTheDocument();
    fireEvent.keyDown(screen.getByRole('menu'), { key: 'Escape' });
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('returns focus to the trigger after Escape', () => {
    renderMenu(managerPerms);
    const trigger = screen.getByLabelText('Быстрое меню');
    fireEvent.click(trigger);
    fireEvent.keyDown(screen.getByRole('menu'), { key: 'Escape' });
    expect(document.activeElement).toBe(trigger);
  });

  it('closes the menu on Tab without trapping focus', () => {
    renderMenu(managerPerms);
    fireEvent.click(screen.getByLabelText('Быстрое меню'));
    fireEvent.keyDown(screen.getByRole('menu'), { key: 'Tab' });
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('hides the trigger for a session with no permissions', () => {
    renderMenu([]);
    expect(screen.queryByLabelText('Быстрое меню')).toBeNull();
  });
});
