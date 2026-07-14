import { afterEach, describe, expect, it, mock } from 'bun:test';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { useState } from 'react';
import { SessionStartForm, createSessionStartSelection } from './SessionStartForm';

afterEach(cleanup);

const tariffs = [{
  tariffVersionId: 'tariff-1', tariffRuleVersionId: 'rule-1', name: 'Standard',
  pricePerMinuteMinorUnits: 50
}];

describe('SessionStartForm', () => {
  it('keeps guest open-tab defaults and emits a valid selection', async () => {
    const onChange = mock(() => {});
    const onValidityChange = mock(() => {});
    render(<I18nProvider><SessionStartForm
      seatName="PC-01" currencyCode="TJS" disabled={false}
      value={createSessionStartSelection()} onChange={onChange}
      fixedClient={null} loadTariffs={() => new Promise(() => {})} loadPackages={async () => []}
      onValidityChange={onValidityChange}
    /></I18nProvider>);

    expect(screen.getByRole('tab', { name: 'Гость' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('button', { name: 'Открытый счёт' })).toHaveClass('active');
    await waitFor(() => expect(onValidityChange).toHaveBeenLastCalledWith(true, null));
  });

  it('locks a reservation client, supports wallet/package and validates comp reason', async () => {
    const loadTariffs = async () => tariffs;
    const loadPackages = async () => [{ playerPackageId: 'pkg-1', name: 'Night 5h', remainingIncludedSeconds: 10800 }];
    function Harness() {
      const [value, setValue] = useState(createSessionStartSelection('prepaid_wallet'));
      return <SessionStartForm
        seatName="PC-01" currencyCode="TJS" disabled={false} value={value} onChange={setValue}
        fixedClient={{ playerAccountId: 'p1', name: 'Мадина', phoneNumber: '', balanceMinorUnits: 45000, debtMinorUnits: 0 }}
        loadTariffs={loadTariffs}
        loadPackages={loadPackages}
      />;
    }
    render(<I18nProvider><Harness /></I18nProvider>);
    await act(async () => { await Promise.resolve(); });

    expect(screen.queryByRole('combobox', { name: 'Игрок для биллинга' })).toBeNull();
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Тариф для сессии' })).toHaveTextContent('Standard'));
    expect(screen.getByRole('button', { name: 'Открытый счёт' })).toBeDisabled();

    fireEvent.click(screen.getByRole('tab', { name: 'Пакет' }));
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Пакет для сессии' })).toHaveTextContent('Night 5h'));

    fireEvent.click(screen.getByRole('checkbox', { name: 'Комплиментарная сессия' }));
    expect(screen.getByRole('textbox', { name: 'Причина комплиментарной сессии' })).toHaveAttribute('minlength', '8');
    expect(screen.getByRole('button', { name: 'Открытый счёт' })).toBeDisabled();
    expect(screen.getByRole('combobox', { name: 'Тариф для сессии' })).toHaveTextContent('Standard');
    await act(async () => { await Promise.resolve(); });
  });

  it('disables every interactive field while submitting', () => {
    render(<I18nProvider><SessionStartForm
      seatName="PC-01" currencyCode="TJS" disabled value={createSessionStartSelection()}
      onChange={() => {}} fixedClient={null} loadTariffs={() => new Promise(() => {})} loadPackages={async () => []}
    /></I18nProvider>);
    expect(screen.getAllByRole('button').every((button) => button.hasAttribute('disabled'))).toBe(true);
  });
});
