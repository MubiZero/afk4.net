import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashShiftWorkspace } from './CashShiftWorkspace';
import type { ShiftRevenueDto } from '../operatorApiClients';
import { ToastProvider } from '../operatorToast';
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

function renderWs(current: ShiftRevenueDto | null, history: ShiftRevenueDto[] = []) {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <CashShiftWorkspace
          backend={backend}
          branchId="b1"
          currencyCode="TJS"
          revenueClient={{ current: async () => current, history: async () => ({ shifts: history, limit: 20 }) }}
        />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('CashShiftWorkspace', () => {
  // После «Внести» или «Изъять» раздел перечитывает смену. Раньше экран со сверкой, которую
  // кассир только что читал, пропадал под заглушкой до ответа; теперь остаётся на месте.
  it('после собственного действия смена не пропадает, пока идёт перечитывание', async () => {
    let calls = 0;
    const revenueClient = {
      current: () => { calls += 1; return calls === 1 ? Promise.resolve(openShift()) : new Promise<ShiftRevenueDto | null>(() => {}); },
      history: async () => ({ shifts: [], limit: 20 })
    };
    const ui = (shiftNonce: number) => (
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <CashShiftWorkspace backend={backend} branchId="b1" currencyCode="TJS" revenueClient={revenueClient} shiftNonce={shiftNonce} />
        </ToastProvider>
      </I18nProvider>
    );
    const { container, rerender } = render(ui(0));
    await waitFor(() => expect(container.querySelector('.cash-shift-status-card')).toBeTruthy());

    rerender(ui(1));
    await waitFor(() => expect(calls).toBe(2));
    expect(container.querySelector('.cash-shift-status-card')).toBeTruthy();
  });

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

  // Деньги смены и её команды — в шапке раздела, которая теперь стоит и на этой вкладке. Вкладка
  // их не повторяет: ни «Ожидается в кассе», ни итога выручки, ни второго ряда «Внести · Изъять ·
  // Закрыть». До пересчёта «Фактически» и «Расхождение» всегда пусты — сверка живёт в окне закрытия.
  it('не повторяет шапку раздела: без сверки ящика, итога выручки и кнопок смены', async () => {
    renderWs(openShift());
    expect(await screen.findByText('Смена открыта')).toBeInTheDocument();
    expect(screen.getByText('Зарина Н.')).toBeInTheDocument();
    expect(screen.getByText('Выручка смены')).toBeInTheDocument();
    expect(screen.queryByText('4 310 с.')).toBeNull();
    expect(screen.queryByText('13 800 с.')).toBeNull();
    expect(screen.queryByText('не введено')).toBeNull();
    expect(screen.queryByRole('button', { name: /Закрыть смену|Внести|Изъять/ })).toBeNull();
    expect(document.querySelector('.cash-shift-main-grid')).not.toBeNull();
  });

  // Движение наличных — вкладка «Кассовые операции». Короткий повтор здесь со своим «Итого
  // движение» спорил с «Итого по кассе» там.
  it('движение наличных не дублирует вкладку кассовых операций', async () => {
    renderWs(openShift());
    await screen.findByText('Выручка смены');
    expect(screen.queryByText('Движение наличных')).toBeNull();
    expect(screen.queryByText('Итого движение')).toBeNull();
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

  it('выбирает закрытую смену и показывает её в инспекторе', async () => {
    renderWs(openShift(), [closedShift()]);
    fireEvent.click(await screen.findByRole('row', { name: /20\.05\.2026/ }));
    const inspector = document.querySelector('.cash-shift-history-detail')!;
    expect(inspector).toHaveTextContent('2 340 с.');
    expect(inspector).toHaveTextContent('-50 с.');
    expect(screen.queryByLabelText('Детали выбранной записи')).toBeNull();
  });

  it('без открытой смены ведёт последним закрытием, а не пустой сеткой', async () => {
    renderWs(null, [closedShift()]);
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
