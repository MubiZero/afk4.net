import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClientActionsMenu } from './ClientActionsMenu';

afterEach(cleanup);

const renderMenu = (over: Partial<Parameters<typeof ClientActionsMenu>[0]> = {}) => {
  const onEditProfile = mock(() => {});
  const onSetPin = mock(() => {});
  const onToggleActive = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <ClientActionsMenu
        isActive={true}
        canManageClient={true}
        onEditProfile={onEditProfile}
        onSetPin={onSetPin}
        onToggleActive={onToggleActive}
        {...over}
      />
    </I18nProvider>
  );
  return { onEditProfile, onSetPin, onToggleActive };
};

describe('ClientActionsMenu', () => {
  it('hides the menu until opened', () => {
    renderMenu();
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('opens on trigger click and shows items', () => {
    renderMenu();
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    expect(screen.getByRole('menu')).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /Править профиль/ })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /Деактивировать/ })).toBeInTheDocument();
  });

  it('shows reactivate label when client is inactive', () => {
    renderMenu({ isActive: false });
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    expect(screen.getByRole('menuitem', { name: /Активировать/ })).toBeInTheDocument();
  });

  it('calls handler and closes after selecting an item', () => {
    const { onEditProfile } = renderMenu();
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    fireEvent.click(screen.getByRole('menuitem', { name: /Править профиль/ }));
    expect(onEditProfile).toHaveBeenCalled();
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('closes on Escape', () => {
    renderMenu();
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('focuses first menuitem on open', () => {
    renderMenu();
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    const items = screen.getAllByRole('menuitem');
    expect(document.activeElement).toBe(items[0]);
  });

  it('moves focus to next item on ArrowDown', () => {
    renderMenu();
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    const items = screen.getAllByRole('menuitem');
    // Фокус уже на первом пункте (автофокус при открытии).
    fireEvent.keyDown(items[0], { key: 'ArrowDown' });
    expect(document.activeElement).toBe(items[1]);
  });

  it('renders nothing when no permission grants any item', () => {
    renderMenu({ canManageClient: false });
    expect(document.querySelector('.client-actions-menu')).toBeNull();
  });

  it('adds the reservation item first and the correction item before deactivate when permitted', () => {
    const onCreateReservation = mock(() => {});
    const onCorrect = mock(() => {});
    renderMenu({ canCreateReservation: true, onCreateReservation, canCorrect: true, onCorrect });
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    const items = screen.getAllByRole('menuitem');
    expect(items.map((item) => item.textContent)).toEqual([
      'Создать бронь',
      'Править профиль',
      'PIN клиента',
      'Ручная корректировка',
      'Деактивировать',
    ]);
    fireEvent.click(screen.getByRole('menuitem', { name: /Создать бронь/ }));
    expect(onCreateReservation).toHaveBeenCalled();
  });

  it('shows a separator before the deactivate item once another item is present', () => {
    renderMenu({ canCorrect: true, onCorrect: () => {} });
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    expect(screen.getByRole('separator')).toBeInTheDocument();
  });

  it('omits the reservation/correction items when their permission is missing', () => {
    renderMenu({ canCreateReservation: false, canCorrect: false });
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    expect(screen.queryByRole('menuitem', { name: /Создать бронь/ })).toBeNull();
    expect(screen.queryByRole('menuitem', { name: /Ручная корректировка/ })).toBeNull();
  });
});
