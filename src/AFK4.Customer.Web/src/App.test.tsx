import { it, expect, beforeEach, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { App } from './App';

beforeEach(() => { globalThis.localStorage?.clear(); });

function setSignedInSession() {
  localStorage.setItem('afk4.player.session', JSON.stringify({
    playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф', phoneVerified: true,
    accessToken: 'tok', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'ref', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
  }));
}

// Every /api/me/* call this render makes shares one body, except /api/me/features which answers
// per-test: dashboard reads walletBalance/debtBalance/activeSession, list endpoints read
// items/nextCursor, array endpoints see extra fields.
function mockFetchWithFeatures(featuresOutcome: { ok: true; features: string[] } | { ok: false }) {
  const dashboardBody = JSON.stringify({
    walletBalance: { currencyCode: 'TJS', minorUnits: 0 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: null,
    items: [], nextCursor: null
  });
  return mock().mockImplementation(async (url: unknown) => {
    if (typeof url === 'string' && url.includes('/api/me/features')) {
      if (!featuresOutcome.ok) return { ok: false, status: 500, text: async () => '{}' };
      return { ok: true, status: 200, text: async () => JSON.stringify({ features: featuresOutcome.features }) };
    }
    return { ok: true, status: 200, text: async () => dashboardBody };
  });
}

it('shows the sign-in screen when there is no session', () => {
  render(<I18nProvider><App /></I18nProvider>);
  expect(screen.getByRole('button', { name: 'Войти' })).toBeInTheDocument();
});

it('shows the app shell + dashboard tab when a session exists', () => {
  globalThis.localStorage?.setItem('afk4.player.session', JSON.stringify({
    playerAccountId: 'p1', organizationId: 'org1', displayName: 'Фёдор', phoneVerified: true,
    accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
  }));
  render(<I18nProvider><App /></I18nProvider>);
  expect(screen.getByRole('navigation')).toBeInTheDocument();
  expect(screen.getByText('Главная')).toBeInTheDocument();
});

it('navigates to the reservations tab and renders its screen', async () => {
  localStorage.setItem('afk4.player.session', JSON.stringify({
    playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф', phoneVerified: false,
    accessToken: 'tok', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'ref', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
  }));
  // One combined body satisfies every call this render makes: dashboard reads walletBalance/
  // debtBalance/activeSession; list endpoints read items/nextCursor; array endpoints see extra
  // fields; getFeatures reads features (all enabled here, so the reservations tab stays visible).
  const body = JSON.stringify({
    walletBalance: { currencyCode: 'TJS', minorUnits: 0 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: null,
    items: [], nextCursor: null,
    features: ['online_booking', 'loyalty', 'online_topup', 'player_shop']
  });
  globalThis.fetch = mock().mockResolvedValue({ ok: true, status: 200, text: async () => body }) as unknown as typeof fetch;
  render(<I18nProvider><App /></I18nProvider>);
  fireEvent.click(await screen.findByRole('button', { name: 'Брони' }));
  await waitFor(() => expect(screen.getByRole('heading', { name: 'Брони' })).toBeInTheDocument());
  localStorage.clear();
});

it('прячет вкладку «Брони», когда онлайн-бронирование выключено', async () => {
  setSignedInSession();
  globalThis.fetch = mockFetchWithFeatures({ ok: true, features: ['loyalty', 'online_topup', 'player_shop'] }) as unknown as typeof fetch;
  render(<I18nProvider><App /></I18nProvider>);
  const nav = await screen.findByRole('navigation');
  await waitFor(() => expect(within(nav).queryByRole('button', { name: 'Брони' })).not.toBeInTheDocument());
  expect(within(nav).getAllByRole('button')).toHaveLength(3);
});

it('показывает вкладку «Брони», когда фича включена', async () => {
  setSignedInSession();
  globalThis.fetch = mockFetchWithFeatures({ ok: true, features: ['online_booking', 'loyalty', 'online_topup', 'player_shop'] }) as unknown as typeof fetch;
  render(<I18nProvider><App /></I18nProvider>);
  const nav = await screen.findByRole('navigation');
  expect(await within(nav).findByRole('button', { name: 'Брони' })).toBeInTheDocument();
  expect(within(nav).getAllByRole('button')).toHaveLength(4);
});

it('не роняет экран, если список фич не пришёл', async () => {
  setSignedInSession();
  globalThis.fetch = mockFetchWithFeatures({ ok: false }) as unknown as typeof fetch;
  render(<I18nProvider><App /></I18nProvider>);
  // getFeatures отклонён (сеть/сервер недоступны) → фичи считаются включёнными (fail-open, см.
  // App.tsx): экран не падает и «Брони» остаётся в навигации, а не пропадает из-за сбоя загрузки.
  expect(await screen.findByRole('navigation')).toBeInTheDocument();
  expect(screen.getByText('Главная')).toBeInTheDocument();
  expect(await screen.findByRole('button', { name: 'Брони' })).toBeInTheDocument();
});
