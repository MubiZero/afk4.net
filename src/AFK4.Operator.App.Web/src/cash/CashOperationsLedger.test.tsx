import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashOperationsLedger } from './CashOperationsLedger';

afterEach(cleanup);
const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't' }, branchId: 'b1' } as never;
const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });

function renderLedger(rows: Record<string, unknown>[]) {
  render(
    <I18nProvider initialLocale="ru">
      <CashOperationsLedger
        backend={backend}
        branchId="b1"
        currencyCode="TJS"
        reports={{ getCashOperationReport: async () => ({ rows, cashInTotal: m(5000), cashOutTotal: m(2000), netCashTotal: m(3000) }) }}
      />
    </I18nProvider>
  );
}

const rows = [
  { operationId: 'c1', createdAtUtc: '2026-06-24T10:00:00Z', operationType: 'cash_in', cashImpact: m(5000), reason: 'Размен кассы', sourceType: 'shift' },
  { operationId: 'c2', createdAtUtc: '2026-06-24T11:00:00Z', operationType: 'cash_out', cashImpact: m(-2000), reason: 'Инкассация', sourceType: 'shift' }
];

describe('CashOperationsLedger', () => {
  it('рендерит строки операций и сводку', async () => {
    renderLedger(rows);
    await waitFor(() => expect(screen.getByText('Размен кассы')).toBeInTheDocument());
    expect(screen.getByText('Инкассация')).toBeInTheDocument();
    expect(screen.getByText('Итого по кассе')).toBeInTheDocument();
  });

  it('поиск фильтрует по причине', async () => {
    renderLedger(rows);
    await waitFor(() => expect(screen.getByText('Размен кассы')).toBeInTheDocument());
    fireEvent.change(screen.getByPlaceholderText('Поиск по причине или типу'), { target: { value: 'инкасс' } });
    expect(screen.queryByText('Размен кассы')).toBeNull();
    expect(screen.getByText('Инкассация')).toBeInTheDocument();
  });

  it('пустой набор → пустое состояние', async () => {
    renderLedger([]);
    await waitFor(() => expect(screen.getByText('Кассовых операций нет')).toBeInTheDocument());
  });

  it('провал экспорта показывает inline-нотис ошибки', async () => {
    // config:'x' роняет построение боевого клиента в exportCsv → попадает в catch.
    const brokenBackend = { config: 'x', session: 's', branchId: 'b' } as never;
    render(
      <I18nProvider initialLocale="ru">
        <CashOperationsLedger
          backend={brokenBackend}
          branchId="b"
          currencyCode="TJS"
          reports={{ getCashOperationReport: async () => ({ rows: [] }) }}
        />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Кассовых операций нет')).toBeInTheDocument());
    expect(document.querySelector('.cash-export-error')).toBeNull(); // в норме нотиса нет
    fireEvent.click(screen.getByRole('button', { name: /Экспорт/i }));
    await waitFor(() => expect(document.querySelector('.cash-export-error')).not.toBeNull());
  });
});
