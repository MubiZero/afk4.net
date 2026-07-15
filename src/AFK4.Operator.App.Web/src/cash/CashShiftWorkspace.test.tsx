import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashShiftWorkspace } from './CashShiftWorkspace';
import type { ShiftRevenueDto } from '../operatorApiClients';

afterEach(cleanup);
const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });

function openShift(): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o', branchId: 'b1',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: m(310000), goods: m(115000), total: m(425000) },
    inflow: { cash: m(200000), nonCash: m(180000), walletTopUps: m(90000), directTotal: m(380000) },
    cash: { starting: m(1000000), expected: m(1380000), counted: null, difference: null },
    openedAtUtc: '2026-06-24T09:00:00Z', closedAtUtc: null
  };
}

const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't' }, branchId: 'b1' } as never;

function closedShift(): ShiftRevenueDto {
  return {
    ...openShift(),
    shiftId: 'closed-1',
    state: 'closed',
    earned: { time: m(150000), goods: m(84000), total: m(234000) },
    cash: { starting: m(100000), expected: m(263800), counted: m(258800), difference: m(-5000) },
    openedAtUtc: '2026-05-20T08:00:00Z',
    closedAtUtc: '2026-05-20T22:00:00Z'
  };
}

function renderWs(current: ShiftRevenueDto | null, cashRows: Record<string, unknown>[] = [], history: ShiftRevenueDto[] = []) {
  render(
    <I18nProvider initialLocale="ru">
      <CashShiftWorkspace
        backend={backend}
        branchId="b1"
        currencyCode="TJS"
        revenueClient={{ current: async () => current, history: async () => ({ shifts: history, limit: 20 }) }}
        reports={{ getCashOperationReport: async () => ({ rows: cashRows }) }}
      />
    </I18nProvider>
  );
}

describe('CashShiftWorkspace', () => {
  it('открытая смена → выручка и сверка', async () => {
    renderWs(openShift());
    await waitFor(() => expect(screen.getByText('Выручка смены')).toBeInTheDocument());
    expect(screen.getByText('Сверка кассы')).toBeInTheDocument();
    expect(screen.getByText('Ожидается')).toBeInTheDocument();
  });

  it('нет смены → пустое состояние', async () => {
    renderWs(null);
    await waitFor(() => expect(screen.getByText('Нет открытой смены')).toBeInTheDocument());
  });

  it('открытая смена → «Расхождение» показывает «Смена не закрыта», не «0 с.»', async () => {
    renderWs(openShift()); // difference === null
    await waitFor(() => expect(screen.getByText('Сверка кассы')).toBeInTheDocument());
    const rows = screen.getAllByText('Смена не закрыта');
    // Должны быть минимум две строки «Смена не закрыта» (Посчитано + Расхождение)
    expect(rows.length).toBeGreaterThanOrEqual(2);
    // Строка «Расхождение» НЕ должна содержать «0,00» (formatMoney(null) давал «0,00 с.»)
    const differenceLabels = screen.getAllByText('Расхождение');
    expect(differenceLabels).toHaveLength(1);
    const differenceRow = differenceLabels[0].closest('div')!;
    expect(differenceRow.textContent).not.toMatch(/0[,.]00/);
  });

  it('последние движения наличных в списке', async () => {
    renderWs(openShift(), [
      { operationId: 'c1', createdAtUtc: '2026-06-24T10:00:00Z', operationType: 'cash_in', cashImpact: m(5000), reason: 'Размен' }
    ]);
    await waitFor(() => expect(screen.getByText('Движение наличных')).toBeInTheDocument());
  });

  it('показывает компактные показатели, формулу сверки и причину движения', async () => {
    renderWs(openShift(), [
      { operationId: 'c1', createdAtUtc: '2026-06-24T10:00:00Z', operationType: 'cash_in', cashImpact: m(5000), reason: 'Разменный фонд' }
    ]);
    expect(await screen.findByLabelText('Ключевые показатели смены')).toBeInTheDocument();
    expect(screen.getByText('Старт + наличные продажи + внесения − изъятия − возвраты')).toBeInTheDocument();
    expect(screen.getByText('Разменный фонд')).toBeInTheDocument();
  });

  it('выбирает закрытую смену и показывает её в инспекторе', async () => {
    renderWs(openShift(), [], [closedShift()]);
    fireEvent.click(await screen.findByRole('row', { name: /20\.05\.2026/ }));
    const inspector = screen.getByLabelText('Детали выбранной записи');
    expect(inspector).toHaveTextContent('2 340 с.');
    expect(inspector).toHaveTextContent('-50 с.');
  });

  it('без открытой смены ведёт последним закрытием, а не пустой сеткой', async () => {
    renderWs(null, [], [closedShift()]);
    expect(await screen.findByText('Сейчас нет открытой смены')).toBeInTheDocument();
    expect(screen.getByText('Последняя закрытая смена')).toBeInTheDocument();
  });

  it('провал экспорта показывает inline-нотис ошибки', async () => {
    // config:'x' роняет построение боевого клиента в exportCsv → попадает в catch.
    const brokenBackend = { config: 'x', session: 's', branchId: 'b' } as never;
    const empty = {
      current: async () => openShift(),
      history: async () => ({ shifts: [], limit: 20 })
    };
    render(
      <I18nProvider initialLocale="ru">
        <CashShiftWorkspace
          backend={brokenBackend}
          branchId="b"
          currencyCode="TJS"
          revenueClient={empty}
          reports={{ getCashOperationReport: async () => ({ rows: [] }) }}
        />
      </I18nProvider>
    );
    // Дождаться окончания загрузки — кнопки экспорта появляются после рендера смены.
    // Кнопка «Сводка смены» = op.cash.shift.exportShiftSummary.
    const exportBtn = await screen.findByRole('button', { name: /Сводка смены/i });
    expect(document.querySelector('.cash-export-error')).toBeNull();
    fireEvent.click(exportBtn);
    await waitFor(() => expect(document.querySelector('.cash-export-error')).not.toBeNull());
  });
});
