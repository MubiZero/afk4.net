import { afterEach, describe, expect, it, jest } from 'bun:test';
import { act, cleanup, render, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { Loading, SKELETON_DELAY_MS } from './skeletons';
import { PlansTab } from '@/platform/billing/PlansTab';
import { InvoicesTab } from '@/platform/billing/InvoicesTab';
import { PayableQueue } from '@/platform/billing/PayableQueue';
import { AnalyticsTab } from '@/platform/billing/AnalyticsTab';
import { HealthScreen } from '@/platform/health/HealthScreen';
import { UpdatesScreen } from '@/platform/updates/UpdatesScreen';
import { OrganizationHistoryTab } from '@/platform/organizations/OrganizationHistoryTab';
import { OrganizationHealthSection } from '@/platform/organizations/OrganizationHealthSection';
import { SupportAccessSection } from '@/platform/organizations/SupportAccessSection';

afterEach(cleanup);

// Клиент, который никогда не отвечает: экран остаётся в ожидании, и видно ровно то, что стоит на
// месте содержимого.
const silent = new Proxy({}, { get: () => () => new Promise(() => {}) }) as never;

const renderRu = (ui: ReactNode) => render(<I18nProvider><ToastProvider>{ui}</ToastProvider></I18nProvider>);
const columns = (root: ParentNode) => root.querySelectorAll('[data-skeleton="table"] thead th').length;
const shape = (root: ParentNode, kind: string) => root.querySelector(`[data-skeleton="${kind}"]`);

describe('Loading', () => {
  // Быстрый ответ не должен мигать ожиданием: пятая доля секунды — граница, за которой человек
  // замечает паузу, а до неё заглушка только дёргает экран. Число то же, что в Панели AFK4.net.
  // Часы поддельные: на настоящих таймер «через 120 мс» под нагрузкой срабатывал через секунды,
  // когда заглушка уже честно стояла, и тест падал на исправном коде.
  it('holds the shape back for 180 ms so quick answers do not flash', () => {
    expect(SKELETON_DELAY_MS).toBe(180);
    jest.useFakeTimers();
    try {
      const { container } = render(<Loading><div data-skeleton="table" /></Loading>);
      expect(shape(container, 'table')).toBeNull();
      act(() => { jest.advanceTimersByTime(179); });
      expect(shape(container, 'table')).toBeNull();
      act(() => { jest.advanceTimersByTime(1); });
      expect(shape(container, 'table')).not.toBeNull();
    } finally {
      jest.useRealTimers();
    }
  });
});

// Какой экран какую форму получает: число колонок — как у настоящей таблицы, плитки и графики —
// в своих контейнерах.
describe('screens wait in the shape of their content', () => {
  it('plans: a card with a five-column table and the create button', async () => {
    const { container } = renderRu(<PlansTab client={silent} canManage />);
    await waitFor(() => expect(shape(container, 'table')).not.toBeNull());
    expect(columns(container)).toBe(5);
    expect(container.querySelector('.management-panel .mgmt-section-title .skeleton-control')).not.toBeNull();
  });

  it('plans without the right: no button placeholder in the card head', async () => {
    const { container } = renderRu(<PlansTab client={silent} canManage={false} />);
    await waitFor(() => expect(shape(container, 'table')).not.toBeNull());
    expect(container.querySelector('.mgmt-section-title .skeleton-control')).toBeNull();
  });

  // Фильтры от ответа не зависят и стоят на месте уже во время загрузки.
  it('invoices: the real filters above a seven-column table', async () => {
    const { container } = renderRu(<InvoicesTab client={silent} />);
    await waitFor(() => expect(shape(container, 'table')).not.toBeNull());
    expect(columns(container)).toBe(7);
    expect(container.querySelector('.pc-filters input')).not.toBeNull();
  });

  it('payable queue: queue rows, not table rows', async () => {
    const { container } = renderRu(<PayableQueue client={silent} canManage />);
    await waitFor(() => expect(shape(container, 'list')).not.toBeNull());
    expect(container.querySelectorAll('.pc-queue > .pc-queue-row').length).toBeGreaterThan(0);
  });

  it('analytics: four tiles and two charts of the real heights', async () => {
    const { container } = renderRu(<AnalyticsTab client={silent} />);
    await waitFor(() => expect(shape(container, 'tiles')).not.toBeNull());
    expect(container.querySelectorAll('.pc-analytics-summary > .pc-analytics-tile')).toHaveLength(4);
    const heights = [...container.querySelectorAll<HTMLElement>('[data-skeleton="chart"] .skeleton-block')].map(block => block.style.height);
    expect(heights).toEqual(['260px', '220px']);
  });

  it('health: cards of queue rows', async () => {
    const { container } = renderRu(<HealthScreen client={silent} canSendTestEmail={false} />);
    await waitFor(() => expect(shape(container, 'list')).not.toBeNull());
    expect(container.querySelectorAll('.management-panel .pc-queue').length).toBeGreaterThan(1);
  });

  // Шапка экрана (описание и кнопка по праву) стоит сразу, как встанет над таблицей.
  it('updates: the full page head above a six-column table', async () => {
    const { container, getByRole } = renderRu(<UpdatesScreen client={silent} organizationsClient={silent} canManagePackages canManageRollouts />);
    await waitFor(() => expect(shape(container, 'table')).not.toBeNull());
    expect(columns(container)).toBe(6);
    expect(getByRole('button', { name: /зарегистрировать/i })).toBeInTheDocument();
  });

  it('organization history: a five-column table', async () => {
    const { container } = renderRu(<OrganizationHistoryTab client={silent} organizationId="org-1" />);
    await waitFor(() => expect(shape(container, 'table')).not.toBeNull());
    expect(columns(container)).toBe(5);
  });

  it('organization health: four facts and the recent errors table', async () => {
    const { container } = renderRu(<OrganizationHealthSection client={silent} organizationId="org-1" />);
    await waitFor(() => expect(shape(container, 'tiles')).not.toBeNull());
    expect(container.querySelectorAll('.pc-facts > .pc-fact')).toHaveLength(4);
    expect(columns(container)).toBe(5);
  });

  // Раньше на месте списка стояла строка «Загрузка…». Форма выдачи доступа от ответа не зависит и
  // стоит сразу, а под ней ждёт строка доступа: кто, зачем, до какого времени и кнопка.
  it('support access: the issue form now, and a grant row in the list', async () => {
    const { container } = renderRu(<SupportAccessSection client={silent} organizationId="org-1" />);
    await waitFor(() => expect(shape(container, 'list')).not.toBeNull());
    const row = container.querySelector('.ui-list[data-skeleton="list"] > li')!;
    expect(row.querySelectorAll(':scope > p')).toHaveLength(3);
    expect(row.querySelector('.skeleton-control')).not.toBeNull();
    expect(container.querySelector('textarea')).not.toBeNull();
    expect(container.textContent).not.toMatch(/Загрузка/);
  });
});
