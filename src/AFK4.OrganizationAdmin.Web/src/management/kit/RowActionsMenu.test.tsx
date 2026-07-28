import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { RowActionsMenu } from './RowActionsMenu';

const wrap = (ui: React.ReactNode) => render(<I18nProvider initialLocale="ru">{ui}</I18nProvider>);

afterEach(cleanup);

describe('RowActionsMenu', () => {
  it('renders nothing when there are no actions', () => {
    const { container } = wrap(<RowActionsMenu actions={[]} />);
    expect(container.querySelector('.mgmt-menu-trigger')).toBeNull();
  });

  it('opens on trigger click and invokes the action, then closes', () => {
    const onSelect = mock(() => {});
    wrap(<RowActionsMenu actions={[{ id: 'edit', label: 'Переименовать', onSelect }]} />);
    fireEvent.click(screen.getByRole('button', { name: 'Действия' }));
    const item = screen.getByRole('menuitem', { name: 'Переименовать' });
    fireEvent.click(item);
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('marks danger actions and closes on Escape', () => {
    wrap(
      <RowActionsMenu
        actions={[
          { id: 'edit', label: 'Правка', onSelect: () => {} },
          { id: 'del', label: 'Удалить', onSelect: () => {}, danger: true }
        ]}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Действия' }));
    expect(screen.getByRole('menuitem', { name: 'Удалить' }).className).toContain('is-danger');
    expect(screen.getByRole('separator')).toBeTruthy();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('does not invoke a disabled action', () => {
    const onSelect = mock(() => {});
    wrap(<RowActionsMenu actions={[{ id: 'x', label: 'Нельзя', onSelect, disabled: true }]} />);
    fireEvent.click(screen.getByRole('button', { name: 'Действия' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Нельзя' }));
    expect(onSelect).not.toHaveBeenCalled();
  });
});
