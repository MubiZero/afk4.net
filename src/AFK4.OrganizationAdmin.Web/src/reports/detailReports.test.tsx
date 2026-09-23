import { afterEach, beforeEach, describe, expect, it } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { GameplayTimeReport } from './GameplayTimeReport';
import { OperatorActionsReport } from './OperatorActionsReport';

const originalFetch = globalThis.fetch;
let requestedUrls: string[] = [];

const money = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });

const gameplayReport = {
  limit: 200,
  totalDurationSeconds: 7200,
  totalPackageSeconds: 3600,
  totalBonusSeconds: 1800,
  gameplayRevenueTotal: money(60000),
  rows: [
    {
      sessionId: 'session-1', organizationId: 'org-1', branchId: 'branch-1',
      seatId: 'seat-1', deviceId: 'device-1', createdByStaffUserId: 'staff-1',
      playerKind: 'guest', playerAccountId: null, state: 'ended',
      durationSeconds: 3600, packageSeconds: 0, bonusSeconds: 0, gameplayRevenue: money(20000),
      startedAtUtc: '2026-07-29T10:00:00Z', endedAtUtc: '2026-07-29T11:00:00Z', endsAtUtc: null
    }
  ]
};

const actionsReport = {
  limit: 200,
  totalActionCount: 12,
  rows: [
    {
      actorStaffUserId: 'staff-1', actorDisplayName: 'Cashier One', action: 'reservation.cancel',
      outcome: 'Denied', count: 3, firstAtUtc: '2026-07-29T10:00:00Z', lastAtUtc: '2026-07-29T18:00:00Z'
    }
  ]
};

beforeEach(() => {
  requestedUrls = [];
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = input instanceof Request ? input.url : String(input);
    requestedUrls.push(url);
    if (url.includes('/export.csv')) {
      return new Response('seat,hours\nPC-01,1', { status: 200, headers: { 'Content-Type': 'text/csv' } });
    }
    const body = url.includes('/floor-map')
      ? { branchId: 'branch-1', branchName: 'Главный', zones: [], seats: [{ seatId: 'seat-1', seatName: 'PC-01', zoneId: 'z1', zoneName: 'Зал', sortOrder: 1, state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null }] }
      : url.includes('/reports/gameplay-time')
        ? gameplayReport
        : actionsReport;
    return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
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

describe('GameplayTimeReport', () => {
  it('показывает итоги за период и сессии', async () => {
    render(<I18nProvider initialLocale="ru"><GameplayTimeReport backend={backend} /></I18nProvider>);

    expect(await screen.findByText('Часов игры')).toBeInTheDocument();
    expect(screen.getByText('Выручка за игру')).toBeInTheDocument();
    // 7200 секунд — это два часа, а не «7200».
    expect(screen.getAllByText('2').length).toBeGreaterThan(0);
  });

  // Сессия называет место идентификатором. Имя места лежит в плане зала — без него отчёт не
  // отвечает на свой главный вопрос: какой ПК столько наиграл.
  it('называет место по имени, а не идентификатором', async () => {
    render(<I18nProvider initialLocale="ru"><GameplayTimeReport backend={backend} /></I18nProvider>);

    expect(await screen.findByText('PC-01')).toBeInTheDocument();
    expect(screen.queryByText('seat-1')).not.toBeInTheDocument();
  });

  it('спрашивает у сервера сутки целиком, а не одну полночь', async () => {
    render(<I18nProvider initialLocale="ru"><GameplayTimeReport backend={backend} /></I18nProvider>);

    await screen.findByText('PC-01');
    const reportUrl = requestedUrls.find((url) => url.includes('/reports/gameplay-time'))!;
    expect(reportUrl).toContain('fromUtc=');
    expect(decodeURIComponent(reportUrl)).toContain('T23:59:59.999Z');
  });

  it('выгружает тот же период в CSV', async () => {
    render(<I18nProvider initialLocale="ru"><GameplayTimeReport backend={backend} /></I18nProvider>);

    await screen.findByText('PC-01');
    fireEvent.click(screen.getByRole('button', { name: 'Экспорт CSV' }));

    await waitFor(() => expect(requestedUrls.some((url) => url.includes('/reports/gameplay-time/export.csv'))).toBe(true));
  });
});

describe('OperatorActionsReport', () => {
  it('показывает, кто что делал и чем это кончилось', async () => {
    render(<I18nProvider initialLocale="ru"><OperatorActionsReport backend={backend} /></I18nProvider>);

    expect(await screen.findByText('reservation.cancel')).toBeInTheDocument();
    // Итог приходит машинным словом Denied — на экране оно должно быть по-русски.
    expect(screen.getByText('Отказано')).toBeInTheDocument();
    expect(screen.queryByText('Denied')).not.toBeInTheDocument();
    expect(screen.getByText('Администратор смены')).toBeInTheDocument();
  });

  it('выгружает тот же период в CSV', async () => {
    render(<I18nProvider initialLocale="ru"><OperatorActionsReport backend={backend} /></I18nProvider>);

    await screen.findByText('reservation.cancel');
    fireEvent.click(screen.getByRole('button', { name: 'Экспорт CSV' }));

    await waitFor(() => expect(requestedUrls.some((url) => url.includes('/reports/operator-actions/export.csv'))).toBe(true));
  });
});
