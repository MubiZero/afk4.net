import { afterEach, beforeEach, describe, expect, it } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ReportSchedules } from './ReportSchedules';

const originalFetch = globalThis.fetch;

interface RecordedCall { method: string; path: string; body: unknown }

let calls: RecordedCall[] = [];
let schedules: unknown[] = [];

const backend = {
  config: { platformBaseUrl: 'https://platform.test', currencyCode: 'TJS' },
  session: { organizationId: 'org-1', accessToken: 'token', permissions: [] },
  branchId: 'branch-1'
} as never;

const schedule = (overrides: Record<string, unknown> = {}) => ({
  reportScheduleId: 'schedule-1',
  organizationId: 'org-1',
  branchId: 'branch-1',
  reportType: 'sales',
  frequency: 'daily',
  isActive: true,
  nextRunUtc: '2026-09-16T03:00:00Z',
  lastRunUtc: null,
  createdAtUtc: '2026-09-01T03:00:00Z',
  ...overrides
});

beforeEach(() => {
  calls = [];
  schedules = [schedule()];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    calls.push({
      method: init?.method ?? 'GET',
      path: url.pathname,
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined
    });
    const body = (init?.method ?? 'GET') === 'GET' ? schedules : schedule();
    return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
  }) as typeof fetch;
});

afterEach(() => { cleanup(); globalThis.fetch = originalFetch; });

const wrap = () => render(<I18nProvider initialLocale="ru"><ReportSchedules backend={backend} /></I18nProvider>);

const patchCalls = () => calls.filter((call) => call.method === 'PATCH');

describe('ReportSchedules', () => {
  // Приостановка — то, чего не было ни на одной стороне: `isActive` в ответе был, а переключить
  // его было нечем, и уйти в отпуск значило удалить рассылку и завести её заново.
  it('ставит рассылку на паузу, не трогая частоту', async () => {
    wrap();

    fireEvent.click(await screen.findByRole('button', { name: 'Приостановить' }));

    await waitFor(() => expect(patchCalls()).toHaveLength(1));
    expect(patchCalls()[0].path).toBe('/api/organizations/org-1/branches/branch-1/report-schedules/schedule-1');
    expect(patchCalls()[0].body).toEqual({ organizationId: 'org-1', isActive: false });
  });

  it('возобновляет приостановленную', async () => {
    schedules = [schedule({ isActive: false })];
    wrap();

    fireEvent.click(await screen.findByRole('button', { name: 'Возобновить' }));

    await waitFor(() => expect(patchCalls()).toHaveLength(1));
    expect(patchCalls()[0].body).toEqual({ organizationId: 'org-1', isActive: true });
  });

  it('меняет частоту одной правкой, не трогая паузу', async () => {
    wrap();

    const select = await screen.findByRole('combobox', { name: 'Частота рассылки «Продажи»' });
    fireEvent.change(select, { target: { value: 'weekly' } });

    await waitFor(() => expect(patchCalls()).toHaveLength(1));
    expect(patchCalls()[0].body).toEqual({ organizationId: 'org-1', frequency: 'weekly' });
  });

  // Приостановленная рассылка продолжает занимать своё сочетание, и кнопка «Завести» гаснет.
  // Без этой подсказки владелец не понял бы, почему нужная ему рассылка не заводится.
  it('объясняет, что сочетание занято приостановленной рассылкой', async () => {
    // Тот же отчёт, что выбран в форме по умолчанию, — иначе сочетание не совпадает и подсказке неоткуда взяться.
    schedules = [schedule({ isActive: false, reportType: 'shifts' })];
    wrap();

    expect(await screen.findByText(
      'Такая рассылка уже заведена и сейчас на паузе — её можно возобновить ниже.'
    )).toBeTruthy();
  });

  // Дата следующей отправки у приостановленной рассылки бессмысленна: показывать её значит
  // обещать письмо, которого не будет.
  it('вместо даты показывает пометку о паузе', async () => {
    schedules = [schedule({ isActive: false })];
    wrap();

    expect(await screen.findByText(/на паузе/)).toBeTruthy();
  });
});
