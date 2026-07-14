import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashJournalWorkspace } from './CashJournalWorkspace';
import { ToastProvider } from '../operatorToast';

afterEach(cleanup);

// backend=null → компоненты не строят боевой клиент; сегменты гейтятся правами session.
function renderJournal(permissions: string[]) {
  const session = { permissions, organizationId: 'o' } as never;
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <CashJournalWorkspace backend={null} currencyCode="TJS" session={session} />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('CashJournalWorkspace', () => {
  it('менеджер видит оба сегмента, по умолчанию активны «Кассовые операции»', async () => {
    renderJournal(['reports.view', 'billing.money_action.approve']);
    expect(screen.getByRole('tab', { name: 'Кассовые операции' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Проверка' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('Кассовых операций нет')).toBeInTheDocument());
  });

  it('операции, чеки и антифрод остаются доступны после изменений оплаты', async () => {
    renderJournal(['reports.view', 'receipts.view', 'pos.sales.refund', 'billing.money_action.approve']);

    expect(screen.getByRole('tab', { name: 'Кассовые операции' })).toHaveAttribute('aria-selected', 'true');
    await waitFor(() => expect(screen.getByText('Кассовых операций нет')).toBeInTheDocument());

    fireEvent.click(screen.getByRole('tab', { name: 'Чеки' }));
    expect(screen.getByRole('tab', { name: 'Чеки' })).toHaveAttribute('aria-selected', 'true');

    fireEvent.click(screen.getByRole('tab', { name: 'Проверка' }));
    expect(screen.getByRole('tab', { name: 'Проверка' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getAllByRole('tablist')).toHaveLength(2);
  });

  it('без права approve сегмент «Проверка» скрыт, лента кассовых операций рендерится', async () => {
    renderJournal(['reports.view']);
    // Один сегмент — таббар скрыт, сегмент «Проверка» не рендерится…
    expect(screen.queryByRole('tab', { name: 'Проверка' })).toBeNull();
    // …но сама лента «Кассовые операции» отрисована (пустое состояние от инъект-репортс backend=null).
    await waitFor(() => expect(screen.getByText('Кассовых операций нет')).toBeInTheDocument());
  });

  it('переключение на «Проверка» показывает встроенный ReviewWorkspace', async () => {
    renderJournal(['reports.view', 'billing.money_action.approve']);
    await waitFor(() => expect(screen.getByText('Кассовых операций нет')).toBeInTheDocument());
    fireEvent.click(screen.getByRole('tab', { name: 'Проверка' }));
    // Два tablist'а = внешний (сегменты журнала) + внутренний (сегменты встроенного ReviewWorkspace):
    // доказывает, что встроенный review реально отрисовался, а не пустой фрагмент.
    expect(screen.getAllByRole('tablist')).toHaveLength(2);
  });
});
