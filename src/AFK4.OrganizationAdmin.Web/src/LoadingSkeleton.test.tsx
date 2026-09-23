import { afterEach, beforeEach, describe, expect, it } from 'bun:test';
import { cleanup, render, waitFor } from '@testing-library/react';
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
  it('shows nothing for the first 180 ms, then the shape', async () => {
    const { container } = render(<DeferredSkeleton><div data-skeleton="table" /></DeferredSkeleton>);
    expect(container.querySelector('[data-skeleton]')).toBeNull();
    await new Promise((resolve) => setTimeout(resolve, 120));
    expect(container.querySelector('[data-skeleton]')).toBeNull();
    await new Promise((resolve) => setTimeout(resolve, 120));
    expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy();
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
