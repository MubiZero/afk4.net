import { it, expect, beforeEach } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { App } from './App';

beforeEach(() => { globalThis.localStorage?.clear(); });

it('shows the sign-in screen when there is no session', () => {
  render(<App />);
  expect(screen.getByRole('button', { name: 'Войти' })).toBeInTheDocument();
});

it('shows the app shell + dashboard tab when a session exists', () => {
  globalThis.localStorage?.setItem('afk4.player.session', JSON.stringify({
    playerAccountId: 'p1', organizationId: 'org1', displayName: 'Фёдор', phoneVerified: true,
    accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
  }));
  render(<App />);
  expect(screen.getByRole('navigation')).toBeInTheDocument();
  expect(screen.getByText('Главная')).toBeInTheDocument();
});
