import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { RailAccount } from './RailAccount';

afterEach(cleanup);

function renderWidget(onOpenAccount = () => {}, onSignOut = () => {}) {
  return render(
    <I18nProvider>
      <RailAccount displayName="Оператор смены" onOpenAccount={onOpenAccount} onSignOut={onSignOut} />
    </I18nProvider>
  );
}

describe('RailAccount', () => {
  it('shows the operator name with a collapsed menu by default', () => {
    const { getByText, queryByRole } = renderWidget();
    getByText('Оператор смены');
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
