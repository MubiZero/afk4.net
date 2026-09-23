import { afterEach, beforeEach, describe, expect, it } from 'bun:test';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { I18nProvider } from '@afk4/i18n';
import { SummaryReport } from './SummaryReport';
import { RevenueReport } from './RevenueReport';
import { ShiftCashReport } from './ShiftCashReport';
import { GameplayTimeReport } from './GameplayTimeReport';
import { OperatorActionsReport } from './OperatorActionsReport';

// Смена периода — не новый экран, а тот же отчёт за другие даты. Раньше каждый выбор даты
// подменял весь экран заглушкой вместе с самими полями дат: цифры, которые человек сравнивал,
// пропадали, а поле, в котором он только что выбрал дату, исчезало у него из-под курсора.

const originalFetch = globalThis.fetch;
const money = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });
const period = { fromDate: '2026-07-29', toDate: '2026-07-29', timeZone: 'Asia/Dushanbe', fromUtc: '2026-07-28T19:00:00Z', toUtc: '2026-07-29T19:00:00Z' };
const shift = { shiftId: 'shift-closed', state: 'closed', openedByStaffUserId: 'staff-1', expectedCash: money(10000), countedCash: money(9500), difference: money(-500), cashMovementsTotal: money(0), posCashPaymentsTotal: money(0), posRefundsTotal: money(0), billingCashImpactTotal: money(0), openedAtUtc: '2026-07-29T05:00:00Z', closedAtUtc: '2026-07-29T10:00:00Z' };

// Ответ отчёта за первый запрос — «старые данные», за следующие — «новые». Отличаются числом, по
// которому тест и узнаёт, что сейчас на экране.
function reportBody(url: string, fresh: boolean): unknown {
  const amount = fresh ? 25000 : 10000;
  if (url.includes('/workspace/summary')) {
    return { period, attentionTotalCount: fresh ? 2 : 1, attentionItems: [], figures: { netRevenue: money(amount), gameplayRevenue: money(0), posNetSales: money(0), gameplaySeconds: 0 }, trend: [], activeShift: null };
  }
  if (url.includes('/workspace/shifts-cash')) {
    return { period, shifts: [{ ...shift, expectedCash: money(amount) }], cashOperations: [], cashInTotal: money(0), cashOutTotal: money(0), netCashTotal: money(0) };
  }
  if (url.includes('/workspace/revenue')) {
    return { period, grossRevenue: money(amount), refunds: money(0), netRevenue: money(amount), gameplayRevenue: money(0), gameplaySeconds: 0, posNetSales: money(0), comparison: { previousNetRevenue: money(0), differenceMinorUnits: 0, changePercent: null }, sources: [], paymentMethods: [], operators: [] };
  }
  if (url.includes('/floor-map')) return { branchId: 'branch-1', branchName: 'Главный', zones: [], seats: [] };
  if (url.includes('/reports/gameplay-time')) {
    return { limit: 200, totalDurationSeconds: 0, totalPackageSeconds: 0, totalBonusSeconds: 0, gameplayRevenueTotal: money(amount), rows: [] };
  }
  return { limit: 200, totalActionCount: fresh ? 42 : 7, rows: [] };
}

interface Pending { url: string; resolve: (response: Response) => void }
let pending: Pending[] = [];
let reportRequests = 0;

const isReportRequest = (url: string) => !url.includes('/floor-map');
const ok = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });

beforeEach(() => {
  pending = [];
  reportRequests = 0;
  // Первый запрос отчёта отвечает сразу; следующие ждут, пока тест не отпустит их сам.
  globalThis.fetch = ((input: RequestInfo | URL) => {
    const url = input instanceof Request ? input.url : String(input);
    if (!isReportRequest(url)) return Promise.resolve(ok(reportBody(url, false)));
    reportRequests += 1;
    if (reportRequests === 1) return Promise.resolve(ok(reportBody(url, false)));
    return new Promise<Response>((resolve) => pending.push({ url, resolve }));
  }) as typeof fetch;
});

afterEach(() => { cleanup(); globalThis.fetch = originalFetch; });

const backend = {
  config: { platformBaseUrl: 'https://platform.test', currencyCode: 'TJS' },
  session: {
    staffUserId: 'staff-1', organizationId: 'org-1', displayName: 'Owner',
    permissions: ['organization.reports.view'], accessToken: 'token',
    accessTokenExpiresAtUtc: '2099-01-01T00:00:00Z', refreshToken: 'refresh',
    refreshTokenExpiresAtUtc: '2099-02-01T00:00:00Z', branchIds: ['branch-1']
  },
  branchId: 'branch-1'
} as never;

const renderRu = (ui: ReactNode) => render(<I18nProvider initialLocale="ru">{ui}</I18nProvider>);
const dateInputs = (root: ParentNode) => root.querySelectorAll<HTMLInputElement>('.reports-range input[type="date"]');
const wait = (ms: number) => act(() => new Promise((resolve) => setTimeout(resolve, ms)));

const reports: { name: string; ui: () => ReactNode; oldText: string; newText: string }[] = [
  { name: 'summary', ui: () => <SummaryReport backend={backend} onNavigate={() => {}} />, oldText: 'Требуют проверки: 1', newText: 'Требуют проверки: 2' },
  { name: 'revenue', ui: () => <RevenueReport backend={backend} currencyCode="TJS" />, oldText: '100 с.', newText: '250 с.' },
  { name: 'shifts and cash', ui: () => <ShiftCashReport backend={backend} currencyCode="TJS" />, oldText: '100 с.', newText: '250 с.' },
  { name: 'gameplay time', ui: () => <GameplayTimeReport backend={backend} />, oldText: '100 с.', newText: '250 с.' },
  { name: 'operator actions', ui: () => <OperatorActionsReport backend={backend} />, oldText: '7', newText: '42' }
];

describe('changing the report period refreshes quietly over the data on screen', () => {
  for (const report of reports) {
    it(`${report.name}: keeps the date fields and the old figures until the new ones arrive`, async () => {
      const { container } = renderRu(report.ui());
      await screen.findAllByText(report.oldText);
      const fromField = dateInputs(container)[0];

      fireEvent.change(fromField, { target: { value: '2026-07-01' } });
      await waitFor(() => expect(pending).toHaveLength(1));

      // Тот же самый элемент поля — не заглушка на его месте и не новое поле после перемонтирования.
      expect(dateInputs(container)).toHaveLength(2);
      expect(dateInputs(container)[0]).toBe(fromField);
      expect(fromField.value).toBe('2026-07-01');
      expect(screen.getAllByText(report.oldText).length).toBeGreaterThan(0);
      expect(container.querySelector('[data-skeleton]')).toBeNull();

      // Быстрый ответ не мигает признаком обновления; медленный — показывает его поверх данных.
      expect(container.querySelector('[aria-busy="true"]')).toBeNull();
      await wait(220);
      expect(container.querySelector('.reports-body[aria-busy="true"]')).not.toBeNull();
      expect(screen.getByRole('status')).toHaveTextContent('Обновляем данные…');

      await act(async () => { pending[0].resolve(ok(reportBody(pending[0].url, true))); });
      await screen.findAllByText(report.newText);
      expect(screen.queryAllByText(report.oldText)).toHaveLength(0);
      expect(container.querySelector('[aria-busy="true"]')).toBeNull();
      expect(screen.queryByRole('status')).toBeNull();
      expect(dateInputs(container)[0]).toBe(fromField);
    });
  }

  // Две даты подряд — два запроса. Если первый ответит позже второго, на экране не должен
  // остаться отчёт за период, который человек уже сменил.
  it('ignores an answer for a period that has already been changed again', async () => {
    const { container } = renderRu(<SummaryReport backend={backend} onNavigate={() => {}} />);
    await screen.findByText('Требуют проверки: 1');

    fireEvent.change(dateInputs(container)[0], { target: { value: '2026-07-01' } });
    await waitFor(() => expect(pending).toHaveLength(1));
    fireEvent.change(dateInputs(container)[0], { target: { value: '2026-07-02' } });
    await waitFor(() => expect(pending).toHaveLength(2));

    await act(async () => { pending[1].resolve(ok({ ...(reportBody(pending[1].url, true) as object), attentionTotalCount: 5 })); });
    await screen.findByText('Требуют проверки: 5');
    await act(async () => { pending[0].resolve(ok(reportBody(pending[0].url, true))); });
    await wait(20);
    expect(screen.getByText('Требуют проверки: 5')).toBeInTheDocument();
  });

  // Отказ за новый период не должен отнимать поля дат: без них не вернуться к периоду, который
  // работал, и «Повторить» крутил бы тот же отказ.
  it('a refused refresh names the reason and keeps the date fields', async () => {
    const { container } = renderRu(<SummaryReport backend={backend} onNavigate={() => {}} />);
    await screen.findByText('Требуют проверки: 1');
    const fromField = dateInputs(container)[0];

    fireEvent.change(fromField, { target: { value: '2026-07-01' } });
    await waitFor(() => expect(pending).toHaveLength(1));
    await act(async () => { pending[0].resolve(new Response('{}', { status: 503, statusText: 'Service Unavailable', headers: { 'Content-Type': 'application/json' } })); });

    expect(await screen.findByText('Сервер вернул ошибку. Повторите позже.')).toBeInTheDocument();
    expect(dateInputs(container)[0]).toBe(fromField);
    expect(fromField.value).toBe('2026-07-01');
    // Цифры за прошлый период под новыми датами были бы неправдой.
    expect(screen.queryByText('Требуют проверки: 1')).toBeNull();
  });

  // Поля периода от ответа не зависят и стоят на месте с первой секунды: их можно менять, пока
  // отчёт ещё грузится.
  it('shows the real date fields while the first answer is on its way', async () => {
    globalThis.fetch = (() => new Promise<Response>(() => {})) as unknown as typeof fetch;
    const { container } = renderRu(<SummaryReport backend={backend} onNavigate={() => {}} />);
    await waitFor(() => expect(container.querySelector('.reports-figures[data-skeleton="tiles"]')).toBeTruthy());
    expect(dateInputs(container)).toHaveLength(2);
    expect(screen.getByRole('button', { name: 'Применить' })).toBeInTheDocument();
  });
});
