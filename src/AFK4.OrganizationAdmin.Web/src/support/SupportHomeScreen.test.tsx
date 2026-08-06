import { afterEach, beforeEach, expect, it } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { SupportHomeScreen } from './SupportHomeScreen';
import { readSupportSession, writeSupportSession, type SupportSession } from './supportSession';

const session: SupportSession = {
  sessionToken: 'support-token-1',
  organizationId: 'o1',
  organizationName: 'AFK4 Dushanbe',
  reason: 'Смена не открывается',
  expiresAtUtc: new Date(Date.now() + 5 * 60_000).toISOString(),
  writableAreas: ['branch-settings']
};

beforeEach(() => sessionStorage.clear());
afterEach(() => {
  cleanup();
  sessionStorage.clear();
});

it('shows the organization, reason and writable areas of the active support session', () => {
  render(
    <I18nProvider>
      <SupportHomeScreen session={session} />
    </I18nProvider>
  );

  expect(screen.getByText('AFK4 Dushanbe')).toBeInTheDocument();
  expect(screen.getByText(/Смена не открывается/)).toBeInTheDocument();
  expect(screen.getByText(/branch-settings/)).toBeInTheDocument();
});

it('tells the operator when the grant is view-only (no writable areas)', () => {
  render(
    <I18nProvider>
      <SupportHomeScreen session={{ ...session, writableAreas: [] }} />
    </I18nProvider>
  );

  expect(screen.getByText(/Только просмотр/)).toBeInTheDocument();
});

it('clears the session and reloads once the grant expires', async () => {
  writeSupportSession(session);
  const originalReload = window.location.reload;
  let reloaded = false;
  // jsdom doesn't implement navigation; stub reload so the expiry effect can be observed safely.
  Object.defineProperty(window.location, 'reload', {
    configurable: true,
    value: () => {
      reloaded = true;
    }
  });

  try {
    const almostExpired: SupportSession = {
      ...session,
      expiresAtUtc: new Date(Date.now() + 20).toISOString()
    };

    render(
      <I18nProvider>
        <SupportHomeScreen session={almostExpired} />
      </I18nProvider>
    );

    await new Promise((resolve) => setTimeout(resolve, 1200));

    expect(reloaded).toBe(true);
    expect(readSupportSession()).toBeNull();
  } finally {
    Object.defineProperty(window.location, 'reload', {
      configurable: true,
      value: originalReload
    });
  }
});
