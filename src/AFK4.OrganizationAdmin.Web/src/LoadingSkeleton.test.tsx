import { afterEach, beforeEach, describe, expect, it, jest } from 'bun:test';
import { act, cleanup, render, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from './operatorToast';
import { DeferredSkeleton, gridColumnCount } from './LoadingSkeleton';
import { BranchesDestination } from './network/branches/BranchesDestination';
import { BillingDestination } from './network/billing/BillingDestination';
import { UpdatesDestination } from './network/updates/UpdatesDestination';
import { JournalDestination } from './network/journal/JournalDestination';
import { SummaryReport } from './reports/SummaryReport';
import { RevenueReport } from './reports/RevenueReport';
import { ShiftCashReport } from './reports/ShiftCashReport';
import { OperatorActionsReport } from './reports/OperatorActionsReport';
import { GameplayTimeReport } from './reports/GameplayTimeReport';
import { ReportSchedules } from './reports/ReportSchedules';
import { CashShiftWorkspace } from './cash/CashShiftWorkspace';
import { CashOperationsLedger } from './cash/CashOperationsLedger';
import { CashReceiptsLedger } from './cash/CashReceiptsLedger';
import { CashTopUpRequests } from './cash/CashTopUpRequests';
import { NewsWorkspace } from './NewsWorkspace';
import { EventsWorkspace } from './EventsWorkspace';
import { PackagesSection } from './players/PackagesSection';
import { ClientPackageModal } from './players/ClientPackageModal';
import { ClientSessionModal } from './players/ClientSessionModal';
import { PhoneVerificationCard } from './PhoneVerificationCard';

const originalFetch = globalThis.fetch;
afterEach(() => { cleanup(); globalThis.fetch = originalFetch; });

const renderRu = (ui: ReactNode) => render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);
const headColumns = (root: ParentNode) => root.querySelectorAll('[data-skeleton="table"] .ctable-head > span').length;

describe('gridColumnCount', () => {
  it('counts the columns of a grid template, minmax and repeat included', () => {
    expect(gridColumnCount('1fr')).toBe(1);
    expect(gridColumnCount('1.3fr 1fr 0.9fr 0.8fr')).toBe(4);
    expect(gridColumnCount('minmax(160px, 1fr) minmax(200px, 1.4fr) 140px 120px 160px')).toBe(5);
    expect(gridColumnCount(' 1fr  repeat(2, minmax(0, 1fr)) ')).toBe(2);
  });
});

describe('DeferredSkeleton', () => {
  // Почти все ответы приходят быстрее пятой доли секунды, и заглушка тогда только мигала бы.
  // Часы поддельные: на настоящих таймер «через 120 мс» под нагрузкой срабатывал через секунды,
  // когда заглушка уже честно стояла, и тест падал на исправном коде.
  it('shows nothing for the first 180 ms, then the shape', () => {
    jest.useFakeTimers();
    try {
      const { container } = render(<DeferredSkeleton><div data-skeleton="table" /></DeferredSkeleton>);
      expect(container.querySelector('[data-skeleton]')).toBeNull();
      act(() => { jest.advanceTimersByTime(179); });
      expect(container.querySelector('[data-skeleton]')).toBeNull();
      act(() => { jest.advanceTimersByTime(1); });
      expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy();
    } finally {
      jest.useRealTimers();
    }
  });
});

// Какой экран какую форму получает. Без сервера экраны «Сети» остаются в ожидании, и видно ровно
// то, что стоит на месте содержимого.
describe('network screens wait in the shape of their content', () => {
  it('branches: five totals and a grid of branch cards with four figures each', async () => {
    const { container } = renderRu(<BranchesDestination backend={null} />);
    await waitFor(() => expect(container.querySelector('[data-skeleton="cards"]')).toBeTruthy());
    expect(container.querySelectorAll('.network-branches-totals[data-skeleton="tiles"] > .network-total')).toHaveLength(5);
    const cards = container.querySelectorAll('.network-branches-grid .network-branch-card');
    expect(cards.length).toBeGreaterThan(0);
    for (const card of cards) expect(card.querySelectorAll('.network-branch-kpis .network-stat')).toHaveLength(4);
  });

  it('billing: subscription figures and a five-column invoice table', async () => {
    const { container } = renderRu(<BillingDestination backend={null} />);
    await waitFor(() => expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy());
    expect(container.querySelectorAll('.network-billing-grid[data-skeleton="tiles"] .network-stat')).toHaveLength(5);
    expect(headColumns(container)).toBe(5);
  });

  it('updates: the app state figures and the maintenance window form', async () => {
    const { container } = renderRu(<UpdatesDestination backend={null} />);
    await waitFor(() => expect(container.querySelector('.network-updates-facts[data-skeleton="tiles"]')).toBeTruthy());
    expect(container.querySelector('.network-updates-window-grid[data-skeleton="form"]')).toBeTruthy();
  });

  it('journal: a seven-column audit table', async () => {
    const { container } = renderRu(<JournalDestination backend={null} />);
    await waitFor(() => expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy());
    expect(headColumns(container)).toBe(7);
  });
});

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

// Отчёт ждёт ответа, который не придёт, — на экране остаётся его заглушка.
describe('reports wait in the shape of their content', () => {
  beforeEach(() => {
    globalThis.fetch = (() => new Promise<Response>(() => {})) as unknown as typeof fetch;
  });

  it('summary: the period bar, three figures and seven days', async () => {
    const { container } = renderRu(<SummaryReport backend={backend} onNavigate={() => {}} />);
    await waitFor(() => expect(container.querySelector('.reports-figures[data-skeleton="tiles"]')).toBeTruthy());
    expect(container.querySelector('.reports-range')).toBeTruthy();
    expect(container.querySelectorAll('.reports-figures[data-skeleton="tiles"] > div')).toHaveLength(3);
    expect(container.querySelectorAll('.reports-trend-points > div')).toHaveLength(7);
  });

  it('revenue: three figures, the sources and the breakdowns', async () => {
    const { container } = renderRu(<RevenueReport backend={backend} currencyCode="TJS" />);
    await waitFor(() => expect(container.querySelector('.reports-figures[data-skeleton="tiles"]')).toBeTruthy());
    expect(container.querySelectorAll('.reports-source-split > section')).toHaveLength(2);
    expect(container.querySelectorAll('.reports-breakdowns > section')).toHaveLength(2);
  });

  it('shifts: the shift list next to the inspector', async () => {
    const { container } = renderRu(<ShiftCashReport backend={backend} currencyCode="TJS" />);
    await waitFor(() => expect(container.querySelector('.reports-master-detail[data-skeleton="list"]')).toBeTruthy());
    expect(container.querySelector('.reports-shift-inspector')).toBeTruthy();
  });

  it('operator actions: one figure and a five-column table', async () => {
    const { container } = renderRu(<OperatorActionsReport backend={backend} />);
    await waitFor(() => expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy());
    expect(container.querySelectorAll('.reports-figures[data-skeleton="tiles"] > div')).toHaveLength(1);
    expect(headColumns(container)).toBe(5);
  });

  it('gameplay time: four figures and a five-column table', async () => {
    const { container } = renderRu(<GameplayTimeReport backend={backend} />);
    await waitFor(() => expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy());
    expect(container.querySelectorAll('.reports-figures[data-skeleton="tiles"] > div')).toHaveLength(4);
    expect(headColumns(container)).toBe(5);
  });

  it('schedules: the two-field form and the schedule list', async () => {
    const { container } = renderRu(<ReportSchedules backend={backend} />);
    await waitFor(() => expect(container.querySelector('.mgmt-form[data-skeleton="form"]')).toBeTruthy());
    expect(container.querySelectorAll('.mgmt-form[data-skeleton="form"] > label')).toHaveLength(2);
    expect(container.querySelector('[data-skeleton="list"]')).toBeTruthy();
  });
});

// Экраны, которые до сих пор ждали строкой «Загрузка…». Ответ не приходит, и на экране остаётся
// форма того, что придёт, — а строки ожидания нет вовсе.
describe('cash, news, events and client screens wait in the shape of their content', () => {
  const never = () => new Promise<never>(() => {});
  const noLoadingText = () => expect(document.body.textContent).not.toMatch(/Загрузка|Загружаем/);
  const player = {
    playerAccountId: 'player-1', name: 'Амир К.', isActive: true, status: 'active', balanceMinorUnits: 5000,
    debtMinorUnits: 0, last: '', tone: '', detail: '', phoneNumber: '+992900000001', source: 'backend',
    createdAtUtc: null, lastActivityAtUtc: null, activePackageName: null, activePackageRemainingMinutes: 0,
    platformPersonId: null, createdFromApp: false
  } as never;

  beforeEach(() => {
    globalThis.fetch = (() => new Promise<Response>(() => {})) as unknown as typeof fetch;
  });

  it('shift: status with commands, the drawer check, the revenue strip, cash movements and past shifts', async () => {
    const { container } = renderRu(
      <CashShiftWorkspace backend={null} branchId="branch-1" currencyCode="TJS" revenueClient={{ current: never, history: never }} reports={{ getCashOperationReport: never }} />
    );
    await waitFor(() => expect(container.querySelector('[data-skeleton="cash-shift"]')).toBeTruthy());
    const shape = container.querySelector('[data-skeleton="cash-shift"]')!;
    expect(shape.querySelectorAll('.cash-shift-status-card > .cash-shift-status-block')).toHaveLength(4);
    expect(shape.querySelector('.cash-shift-status-actions .skeleton-control')).toBeTruthy();
    expect(shape.querySelectorAll('.cash-shift-reconcile-band > div')).toHaveLength(3);
    expect(shape.querySelector('.cash-shift-revenue-strip > .cash-shift-revenue-total')).toBeTruthy();
    expect(shape.querySelectorAll('.cash-shift-movement-head > span')).toHaveLength(5);
    const movements = shape.querySelectorAll('.cash-shift-movements > li');
    expect(movements.length).toBeGreaterThan(0);
    for (const row of movements) expect(row.children).toHaveLength(5);
    expect(shape.querySelectorAll('.cash-shift-history-panel .cash-register-row .cash-shift-history-row').length).toBeGreaterThan(0);
    noLoadingText();
  });

  it('cash operations: three figures, the search bar, register rows and the inspector hint as it is', async () => {
    const { container } = renderRu(<CashOperationsLedger backend={null} branchId="branch-1" currencyCode="TJS" reports={{ getCashOperationReport: never }} />);
    await waitFor(() => expect(container.querySelector('[data-skeleton="cash-terminal"]')).toBeTruthy());
    const shape = container.querySelector('.cash-operations-terminal[data-skeleton="cash-terminal"]')!;
    expect(shape.querySelectorAll('.cash-terminal-metrics > .cash-terminal-metric')).toHaveLength(3);
    expect(shape.querySelector('.cash-terminal-register > .cash-ledger-search')).toBeTruthy();
    expect(shape.querySelectorAll('.cash-register-row > .ui-ledger-row.cash-operation-row').length).toBeGreaterThan(0);
    expect(shape.querySelector('.cash-terminal-inspector .cash-inspector-empty')).toHaveTextContent('Выберите операцию, чтобы увидеть детали.');
    noLoadingText();
  });

  it('receipts: three figures and receipt rows, no search bar', async () => {
    const { container } = renderRu(<CashReceiptsLedger backend={backend} branchId="branch-1" currencyCode="TJS" session={null} />);
    await waitFor(() => expect(container.querySelector('[data-skeleton="cash-terminal"]')).toBeTruthy());
    const shape = container.querySelector('.cash-receipts-terminal[data-skeleton="cash-terminal"]')!;
    expect(shape.querySelectorAll('.cash-terminal-metric')).toHaveLength(3);
    expect(shape.querySelector('.cash-ledger-search')).toBeNull();
    const rows = shape.querySelectorAll('.cash-register-row > .cash-receipt-row');
    expect(rows.length).toBeGreaterThan(0);
    for (const row of rows) expect(row.children).toHaveLength(4);
    expect(shape.querySelector('.cash-inspector-empty')).toHaveTextContent('Выберите чек, чтобы увидеть состав и оплату.');
    noLoadingText();
  });

  // Карточка чека грузится отдельно от ленты: список уже на экране, а инспектор ждёт в форме чека.
  it('receipt detail: the receipt card shape with its real section headings', async () => {
    const salesReport = {
      limit: 50, grossSalesTotal: { currencyCode: 'TJS', minorUnits: 1000 }, refundsTotal: { currencyCode: 'TJS', minorUnits: 0 },
      rows: [{ posSaleId: 'sale-1', state: 'paid', createdAtUtc: '2026-09-23T10:00:00Z', total: { currencyCode: 'TJS', minorUnits: 1000 }, lines: [] }]
    };
    const ok = (body: unknown) => Promise.resolve(new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    globalThis.fetch = ((input: RequestInfo | URL) => {
      const url = input instanceof Request ? input.url : String(input);
      if (url.includes('/reports/sales')) return ok(salesReport);
      if (url.includes('/shifts/current')) return ok(null);
      return new Promise<Response>(() => {});
    }) as typeof fetch;
    const withReceipts = { ...(backend as { session: object }).session, permissions: ['organization.receipts.view'] };
    const receiptsBackend = { ...(backend as object), session: withReceipts } as never;
    const { container } = renderRu(<CashReceiptsLedger backend={receiptsBackend} branchId="branch-1" currencyCode="TJS" session={withReceipts as never} />);
    await waitFor(() => expect(container.querySelector('.cash-receipts-terminal:not([data-skeleton]) .cash-register-row')).toBeTruthy());
    (container.querySelector('.cash-register-row') as HTMLElement).click();
    await waitFor(() => expect(container.querySelector('.cash-receipt-inspector[data-skeleton="receipt"]')).toBeTruthy());
    const card = container.querySelector('.cash-receipt-inspector[data-skeleton="receipt"]')!;
    expect(card.querySelector('.cash-receipt-inspector-head')).toBeTruthy();
    expect([...card.querySelectorAll('section > h3')].map((heading) => heading.textContent)).toEqual(['Состав чека', 'Оплата']);
    noLoadingText();
  });

  it('top-up requests: queue rows with time, who, amount and the accept button', async () => {
    const { container } = renderRu(<CashTopUpRequests backend={null} branchId="branch-1" currencyCode="TJS" client={{ listPending: never, confirm: never }} />);
    await waitFor(() => expect(container.querySelector('.cash-topups[data-skeleton="list"]')).toBeTruthy());
    const rows = container.querySelectorAll('.cash-topups[data-skeleton="list"] > .ui-ledger-row.cash-topup-row');
    expect(rows.length).toBeGreaterThan(0);
    for (const row of rows) expect(row.querySelector('.skeleton-control--sm')).toBeTruthy();
    noLoadingText();
  });

  it('news: a four-column table with the create button for someone who may write', async () => {
    const { container } = renderRu(<NewsWorkspace backend={null} canManage />);
    await waitFor(() => expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy());
    expect(headColumns(container)).toBe(4);
    expect(container.querySelector('[data-skeleton="table"] .table-toolbar .skeleton-control')).toBeTruthy();
    noLoadingText();
  });

  it('news without the right: no button placeholder in the toolbar', async () => {
    const { container } = renderRu(<NewsWorkspace backend={null} canManage={false} />);
    await waitFor(() => expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy());
    expect(container.querySelector('.table-toolbar .skeleton-control')).toBeNull();
  });

  it('events: a four-column table', async () => {
    const { container } = renderRu(<EventsWorkspace backend={null} canManage />);
    await waitFor(() => expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy());
    expect(headColumns(container)).toBe(4);
    noLoadingText();
  });

  it('client packages: the real heading over a package placeholder', async () => {
    const { container } = renderRu(<PackagesSection packages={[]} loading />);
    expect(container.querySelector('.clients-packages-section > .eyebrow')).toHaveTextContent('Пакеты клиента');
    await waitFor(() => expect(container.querySelector('.clients-packages-section article[data-skeleton="list"]')).toBeTruthy());
    noLoadingText();
  });

  it('selling a package: the purchase panel with its real title', async () => {
    renderRu(<ClientPackageModal backend={backend} player={player} onClose={() => {}} onPurchased={() => {}} />);
    await waitFor(() => expect(document.querySelector('.pos-package-purchase[data-skeleton="form"]')).toBeTruthy());
    const panel = document.querySelector('.pos-package-purchase[data-skeleton="form"]')!;
    expect(panel.querySelector('strong')).toHaveTextContent('Пакет клиенту');
    expect(panel.querySelectorAll('.skeleton-control')).toHaveLength(2);
    noLoadingText();
  });

  it('seating a client: seat, the start form with its real group headings and the two buttons', async () => {
    renderRu(<ClientSessionModal backend={backend} player={player} currencyCode="TJS" onClose={() => {}} onStarted={() => {}} />);
    await waitFor(() => expect(document.querySelector('.clients-new-form[data-skeleton="form"]')).toBeTruthy());
    const form = document.querySelector('.clients-new-form[data-skeleton="form"]')!;
    expect(form.querySelector(':scope > label')).toHaveTextContent('Место');
    const heads = [...form.querySelectorAll('.start-dialog-body > .start-section-head')].map((head) => head.textContent);
    expect(heads).toEqual(['Кто играет', 'Игрок', 'Списание', 'Тариф', 'Время']);
    expect(form.querySelectorAll('.start-segment.three > button')).toHaveLength(3);
    expect(form.querySelectorAll('.start-duration-chips > button')).toHaveLength(5);
    expect(form.querySelectorAll('.critical-confirmation-actions > button')).toHaveLength(2);
    noLoadingText();
  });

  it('phone verification: the verified-number row', async () => {
    const { container } = renderRu(<PhoneVerificationCard backend={backend} />);
    await waitFor(() => expect(container.querySelector('.account-phone-verified[data-skeleton="phone"]')).toBeTruthy());
    expect(container.querySelector('.account-phone-verified[data-skeleton="phone"] button')).toBeTruthy();
    noLoadingText();
  });
});
