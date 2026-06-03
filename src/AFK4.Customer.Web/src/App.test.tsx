import { it, expect, beforeEach, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { App } from './App';

beforeEach(() => { globalThis.localStorage?.clear(); });

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
  // debtBalance/activeSession; list endpoints read items/nextCursor; array endpoints see extra fields.
  const body = JSON.stringify({
    walletBalance: { currencyCode: 'TJS', minorUnits: 0 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: null,
    items: [], nextCursor: null
  });
  globalThis.fetch = mock().mockResolvedValue({ ok: true, status: 200, text: async () => body }) as unknown as typeof fetch;
  render(<I18nProvider><App /></I18nProvider>);
  fireEvent.click(await screen.findByRole('button', { name: 'Брони' }));
  await waitFor(() => expect(screen.getByRole('heading', { name: 'Брони' })).toBeInTheDocument());
  localStorage.clear();
});
