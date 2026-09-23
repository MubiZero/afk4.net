import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashShiftWorkspace } from './CashShiftWorkspace';
import type { ShiftRevenueDto } from '../operatorApiClients';
import { ToastProvider } from '../operatorToast';
import type { CashOperationReportRowDto } from '../operatorApiClients';
import { cashOperationReport, cashOperationRow } from './cashFixtures';
import { PlatformApiError } from '../platformApi';

afterEach(cleanup);
const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });

function openShift(): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o', branchId: 'b1',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: m(310000), goods: m(115000), noShow: m(6000), total: m(431000) },
    inflow: { cash: m(200000), nonCash: m(180000), walletTopUps: m(90000), directTotal: m(380000) },
    cash: { starting: m(1000000), expected: m(1380000), counted: null, difference: null },
    openedAtUtc: '2026-06-24T09:00:00Z', closedAtUtc: null
  };
}

const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't', displayName: 'Зарина Н.' }, branchId: 'b1' } as never;

function closedShift(): ShiftRevenueDto {
  return {
    ...openShift(),
    shiftId: 'closed-1',
    state: 'closed',
    earned: { time: m(150000), goods: m(84000), noShow: m(0), total: m(234000) },
    cash: { starting: m(100000), expected: m(263800), counted: m(258800), difference: m(-5000) },
    openedAtUtc: '2026-05-20T08:00:00Z',
    closedAtUtc: '2026-05-20T22:00:00Z'
  };
}

function renderWs(current: ShiftRevenueDto | null, cashRows: CashOperationReportRowDto[] = [], history: ShiftRevenueDto[] = []) {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <CashShiftWorkspace
          backend={backend}
          branchId="b1"
          currencyCode="TJS"
          revenueClient={{ current: async () => current, history: async () => ({ shifts: history, limit: 20 }) }}
          reports={{ getCashOperationReport: async () => cashOperationReport(cashRows) }}
        />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('CashShiftWorkspace', () => {
  it('удержания за неявку стоят отдельной величиной в полосе выручки', async () => {
    renderWs(openShift());
    await waitFor(() => expect(screen.getByText('Выручка смены')).toBeInTheDocument());
    expect(screen.getByText('Неявки')).toBeInTheDocument();
    expect(screen.getByText('60 с.')).toBeInTheDocument();
  });

  it('филиал без удержаний не получает вечную нулевую строку', async () => {
    renderWs({ ...openShift(), earned: { time: m(310000), goods: m(115000), noShow: m(0), total: m(425000) } });
    await waitFor(() => expect(screen.getByText('Выручка смены')).toBeInTheDocument());
    expect(screen.queryByText('Неявки')).toBeNull();
  });

  it('открытая смена → выручка и сверка', async () => {
    renderWs(openShift());
    await waitFor(() => expect(screen.getByText('Выручка смены')).toBeInTheDocument());
    expect(screen.getByLabelText('Сверка кассы')).toBeInTheDocument();
    expect(screen.getByText('Ожидается в кассе')).toBeInTheDocument();
  });

  it('строит читаемый командный экран: статус, сверка, выручка и рабочая сетка', async () => {
    renderWs(openShift());
    expect(await screen.findByText('Смена открыта')).toBeInTheDocument();
    expect(screen.getByText('Зарина Н.')).toBeInTheDocument();
    expect(document.querySelector('.cash-shift-reconcile-band')).not.toBeNull();
    expect(document.querySelector('.cash-shift-main-grid')).not.toBeNull();
    expect(screen.queryByLabelText('Ключевые показатели смены')).toBeNull();
  });

  it('прячет выгрузки в компактное меню вместо отдельной панели', async () => {
    renderWs(openShift());
    const menu = await screen.findByText('Экспорт');
    expect(menu.closest('details')).toHaveClass('cash-shift-export-menu');
    fireEvent.click(menu);
    expect(screen.getByRole('button', { name: /Сводка смены/i })).toBeInTheDocument();
  });

  it('нет смены → пустое состояние', async () => {
    renderWs(null);
    await waitFor(() => expect(screen.getByText('Нет открытой смены')).toBeInTheDocument());
  });

  it('открытая смена → «Расхождение» остаётся пустым до ввода факта, не превращается в «0 с.»', async () => {
    renderWs(openShift()); // difference === null
    const reconciliation = await screen.findByLabelText('Сверка кассы');
    expect(reconciliation).toHaveTextContent('не введено');
    const differenceRow = screen.getByText('Расхождение').closest('div')!;
    expect(differenceRow).toHaveTextContent('—');
    expect(differenceRow.textContent).not.toMatch(/0[,.]00/);
  });

  // Колонка «Оператор» отвечает на вопрос «кто взял деньги». Пока имени в строке не было, экран
  // подставлял туда имя того, кто сейчас смотрит, — и каждое движение выглядело его собственным.
  it('в движении наличных стоит тот, кто его провёл, а не тот, кто смотрит', async () => {
    renderWs(openShift(), [
      cashOperationRow({ operationId: 'c1', createdAtUtc: '2026-06-24T10:00:00Z', cashImpact: m(5000), reason: 'Размен', createdByDisplayName: 'Мадина' })
    ]);
    await waitFor(() => expect(screen.getByText('Движение наличных')).toBeInTheDocument());
    expect(screen.getByText('Оператор')).toBeInTheDocument();
    expect(screen.getByText('Мадина')).toBeInTheDocument();
    // «Зарина Н.» — открывшая смену, и она названа ровно один раз: в шапке смены.
    expect(screen.getAllByText('Зарина Н.')).toHaveLength(1);
  });

  it('показывает понятную сверку и полную причину движения', async () => {
    renderWs(openShift(), [
      cashOperationRow({ operationId: 'c1', createdAtUtc: '2026-06-24T10:00:00Z', cashImpact: m(5000), reason: 'Разменный фонд' })
    ]);
    expect(await screen.findByLabelText('Сверка кассы')).toBeInTheDocument();
    expect(screen.getByText('Введите сумму после пересчёта')).toBeInTheDocument();
    expect(screen.getByText('Разменный фонд')).toBeInTheDocument();
  });

  it('выбирает закрытую смену и показывает её в инспекторе', async () => {
    renderWs(openShift(), [], [closedShift()]);
    fireEvent.click(await screen.findByRole('row', { name: /20\.05\.2026/ }));
    const inspector = document.querySelector('.cash-shift-history-detail')!;
    expect(inspector).toHaveTextContent('2 340 с.');
    expect(inspector).toHaveTextContent('-50 с.');
    expect(screen.queryByLabelText('Детали выбранной записи')).toBeNull();
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
        <ToastProvider>
          <CashShiftWorkspace
            backend={brokenBackend}
            branchId="b"
            currencyCode="TJS"
            revenueClient={empty}
            reports={{ getCashOperationReport: async () => cashOperationReport() }}
          />
        </ToastProvider>
      </I18nProvider>
    );
    // Дождаться окончания загрузки — кнопки экспорта появляются после рендера смены.
    // Кнопка «Сводка смены» = op.cash.shift.exportShiftSummary.
    fireEvent.click(await screen.findByText('Экспорт'));
    const exportBtn = screen.getByRole('button', { name: /Сводка смены/i });
    expect(document.querySelector('.cash-export-error')).toBeNull();
    fireEvent.click(exportBtn);
    await waitFor(() => expect(document.querySelector('.cash-export-error')).not.toBeNull());
  });

  // Смена, прошлые смены и движение наличных — три запроса и три панели. Отказ отчёта о
  // наличных не должен стирать открытую смену с её сверкой и кнопкой закрытия, а повтор —
  // перечитывать то, что уже на экране.
  it('отказ движения наличных не прячет смену и повторяет только движение', async () => {
    const current = mock(async () => openShift());
    const history = mock(async () => ({ shifts: [closedShift()], limit: 20 }));
    const getCashOperationReport = mock()
      .mockRejectedValueOnce(new PlatformApiError('boom', 500, 'Internal Server Error', ''))
      .mockResolvedValue(cashOperationReport([
        cashOperationRow({ operationId: 'c1', createdAtUtc: '2026-06-24T10:00:00Z', cashImpact: m(5000), reason: 'Размен', createdByDisplayName: 'Мадина' })
      ]));
    render(
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <CashShiftWorkspace backend={backend} branchId="b1" currencyCode="TJS" revenueClient={{ current, history }} reports={{ getCashOperationReport }} />
        </ToastProvider>
      </I18nProvider>
    );

    expect(await screen.findByText('Выручка смены')).toBeInTheDocument();
    expect(screen.getByRole('row', { name: /20\.05\.2026/ })).toBeInTheDocument();
    expect(screen.getByText(/Не удалось загрузить движение наличных/)).toHaveTextContent('Сервер вернул ошибку. Повторите позже.');

    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByText('Мадина')).toBeInTheDocument();
    expect(getCashOperationReport).toHaveBeenCalledTimes(2);
    expect(current).toHaveBeenCalledTimes(1);
    expect(history).toHaveBeenCalledTimes(1);
  });

  it('отказ прошлых смен называет причину, а открытая смена остаётся', async () => {
    const history = mock()
      .mockRejectedValueOnce(new PlatformApiError('boom', 500, 'Internal Server Error', ''))
      .mockResolvedValue({ shifts: [closedShift()], limit: 20 });
    render(
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <CashShiftWorkspace
            backend={backend}
            branchId="b1"
            currencyCode="TJS"
            revenueClient={{ current: async () => openShift(), history }}
            reports={{ getCashOperationReport: async () => cashOperationReport() }}
          />
        </ToastProvider>
      </I18nProvider>
    );

    expect(await screen.findByText('Выручка смены')).toBeInTheDocument();
    expect(screen.getByText(/Не удалось загрузить прошлые смены/)).toHaveTextContent('Сервер вернул ошибку. Повторите позже.');
    expect(screen.queryByText('Закрытых смен нет')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('row', { name: /20\.05\.2026/ })).toBeInTheDocument();
    expect(history).toHaveBeenCalledTimes(2);
  });
});
