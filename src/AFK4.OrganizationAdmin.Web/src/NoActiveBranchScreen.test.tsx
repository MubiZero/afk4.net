import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { NoActiveBranchScreen } from './NoActiveBranchScreen';

afterEach(cleanup);

function renderScreen(props: Parameters<typeof NoActiveBranchScreen>[0]) {
  render(
    <I18nProvider>
      <NoActiveBranchScreen {...props} />
    </I18nProvider>
  );
}

describe('NoActiveBranchScreen', () => {
  it('names the reason, who assigns a branch and what the staff member can do', () => {
    const onLeave = mock(() => {});
    renderScreen({ mode: 'staff', onRecheck: mock(async () => false), onLeave });

    expect(screen.getByRole('heading', { name: 'Нет активного филиала' })).toBeInTheDocument();
    expect(screen.getByText(/не назначена ни в один филиал/)).toBeInTheDocument();
    expect(screen.getByText(/владелец клуба или управляющий филиалом/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Выйти из аккаунта' }));
    expect(onLeave).toHaveBeenCalledTimes(1);
  });

  it('says so when the recheck still finds no branch, instead of doing nothing visible', async () => {
    const onRecheck = mock(async () => false);
    renderScreen({ mode: 'staff', onRecheck, onLeave: mock(() => {}) });

    fireEvent.click(screen.getByRole('button', { name: 'Проверить снова' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Филиал всё ещё не назначен.');
    expect(onRecheck).toHaveBeenCalledTimes(1);
  });

  it('shows why the recheck failed', async () => {
    const onRecheck = mock(async (): Promise<boolean> => {
      throw new Error('Сервер не ответил');
    });
    renderScreen({ mode: 'staff', onRecheck, onLeave: mock(() => {}) });

    fireEvent.click(screen.getByRole('button', { name: 'Проверить снова' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Сервер не ответил');
  });

  it('in support mode points at the organization and exits support mode, not the staff account', () => {
    const onLeave = mock(() => {});
    renderScreen({ mode: 'support', onLeave });

    expect(screen.getByRole('heading', { name: 'Нет активного филиала' })).toBeInTheDocument();
    expect(screen.getByText(/нет ни одного филиала/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Проверить снова' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Выйти из аккаунта' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Выйти из режима поддержки' }));
    expect(onLeave).toHaveBeenCalledTimes(1);
  });
});
