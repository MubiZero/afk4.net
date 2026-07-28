import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { RailAccount } from './RailAccount';

afterEach(cleanup);

const openShift = { tone: 'open' as const, value: '13:00', full: 'Смена открыта · с 13:00' };

function renderWidget(onOpenAccount = () => {}, onSignOut = () => {}, shift = openShift) {
  return render(
    <I18nProvider>
      <RailAccount displayName="Оператор смены" shift={shift} onOpenAccount={onOpenAccount} onSignOut={onSignOut} />
    </I18nProvider>
  );
}

describe('RailAccount', () => {
  it('shows the shift badge value under the avatar with a collapsed menu by default', () => {
    const { getByText, getByTitle, queryByRole } = renderWidget();
    getByText('13:00');
    getByTitle(/Смена открыта/);
    expect(queryByRole('menu')).toBeNull();
  });

  it('opens the account menu with profile and sign-out actions', () => {
    const { getByRole } = renderWidget();
    fireEvent.click(getByRole('button', { name: 'Мой аккаунт' }));
    expect(getByRole('menu')).not.toBeNull();
    getByRole('menuitem', { name: /Мой аккаунт/ });
    getByRole('menuitem', { name: /Выйти/ });
  });

  it('triggers the profile panel and closes the menu', () => {
    const onOpenAccount = mock(() => {});
    const { getByRole, queryByRole } = renderWidget(onOpenAccount);
    fireEvent.click(getByRole('button', { name: 'Мой аккаунт' }));
    fireEvent.click(getByRole('menuitem', { name: /Мой аккаунт/ }));
    expect(onOpenAccount).toHaveBeenCalledTimes(1);
    expect(queryByRole('menu')).toBeNull();
  });

  it('signs out from the menu', () => {
    const onSignOut = mock(() => {});
    const { getByRole } = renderWidget(() => {}, onSignOut);
    fireEvent.click(getByRole('button', { name: 'Мой аккаунт' }));
    fireEvent.click(getByRole('menuitem', { name: /Выйти/ }));
    expect(onSignOut).toHaveBeenCalledTimes(1);
  });
});
