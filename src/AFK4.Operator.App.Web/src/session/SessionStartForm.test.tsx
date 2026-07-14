import { afterEach, describe, expect, it, mock } from 'bun:test';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { useState } from 'react';
import { SessionStartForm, createSessionStartSelection } from './SessionStartForm';
import { PlatformApiError } from '../platformApi';

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
    const onValidityChange = mock(() => {});
    function Harness() {
      const [value, setValue] = useState(createSessionStartSelection('prepaid_wallet'));
      return <SessionStartForm
        seatName="PC-01" currencyCode="TJS" disabled={false} value={value} onChange={setValue}
        fixedClient={{ playerAccountId: 'p1', name: 'Мадина', phoneNumber: '', balanceMinorUnits: 45000, debtMinorUnits: 0 }}
        loadTariffs={loadTariffs}
        loadPackages={loadPackages}
        onValidityChange={onValidityChange}
      />;
    }
    render(<I18nProvider><Harness /></I18nProvider>);
    await act(async () => { await Promise.resolve(); });

    expect(screen.queryByRole('combobox', { name: 'Игрок для биллинга' })).toBeNull();
    expect(screen.getByText('Мадина')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Тариф для сессии' })).toHaveTextContent('Standard'));
    await waitFor(() => expect(onValidityChange).toHaveBeenLastCalledWith(true, null));
    expect(screen.getByRole('button', { name: 'Открытый счёт' })).toBeDisabled();

    fireEvent.click(screen.getByRole('tab', { name: 'Постоплата' }));
    await waitFor(() => expect(onValidityChange).toHaveBeenLastCalledWith(true, null));

    fireEvent.click(screen.getByRole('tab', { name: 'Пакет' }));
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Пакет для сессии' })).toHaveTextContent('Night 5h'));
    await waitFor(() => expect(onValidityChange).toHaveBeenLastCalledWith(true, null));

    fireEvent.click(screen.getByRole('checkbox', { name: 'Комплиментарная сессия' }));
    expect(screen.getByRole('textbox', { name: 'Причина комплиментарной сессии' })).toHaveAttribute('minlength', '8');
    await waitFor(() => expect(onValidityChange).toHaveBeenLastCalledWith(false, expect.any(String)));
    fireEvent.change(screen.getByRole('textbox', { name: 'Причина комплиментарной сессии' }), { target: { value: 'manager courtesy' } });
    await waitFor(() => expect(onValidityChange).toHaveBeenLastCalledWith(true, null));
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

  it('does not expose raw tariff-loader failures to the operator', async () => {
    render(<I18nProvider><SessionStartForm
      seatName="PC-01" currencyCode="TJS" disabled={false} value={createSessionStartSelection()}
      onChange={() => {}} fixedClient={null} loadTariffs={async () => { throw new Error('internal secret'); }} loadPackages={async () => []}
    /></I18nProvider>);
    const alert = await screen.findByRole('alert');
    expect(alert).not.toHaveTextContent('internal secret');
    expect(alert.textContent?.trim().length).toBeGreaterThan(0);
  });

  it('does not expose raw package-loader failures to the operator', async () => {
    render(<I18nProvider><SessionStartForm
      seatName="PC-01" currencyCode="TJS" disabled={false}
      value={{ ...createSessionStartSelection('package'), playerPackageId: null }} onChange={() => {}}
      fixedClient={{ playerAccountId: 'p1', name: 'Мадина', phoneNumber: '', balanceMinorUnits: null, debtMinorUnits: 0 }}
      loadTariffs={async () => tariffs} loadPackages={async () => { throw new Error('package database secret'); }}
    /></I18nProvider>);
    const alert = await screen.findByRole('alert');
    expect(alert).not.toHaveTextContent('package database secret');
    expect(alert.textContent?.trim().length).toBeGreaterThan(0);
  });

  it('uses the localized fallback for an unknown platform loader error', async () => {
    const raw = 'Platform API returned 500: internal database detail';
    render(<I18nProvider><SessionStartForm
      seatName="PC-01" currencyCode="TJS" disabled={false} value={createSessionStartSelection()}
      onChange={() => {}} fixedClient={null}
      loadTariffs={async () => { throw new PlatformApiError(raw, 500, 'Internal Server Error', '{"code":"unknown_internal"}'); }}
      loadPackages={async () => []}
    /></I18nProvider>);
    expect(await screen.findByRole('alert')).not.toHaveTextContent(raw);
  });
});
