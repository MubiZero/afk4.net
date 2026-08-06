import { afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';

// This renders the real, full App — same class of test as App.test.tsx, and it inherits the same
// hazard documented in test/setup.ts: several sibling test files install a process-wide
// mock.module('./operatorHelpers'/'./operatorApiClients', ...) that bun never restores, and that
// stub can still be "in effect" when this file's real createAuthenticatedOperatorClients() runs,
// crashing on whatever sub-client that stub omitted (e.g. `.shifts`). App.test.tsx already solved
// this by running in its own bun invocation, isolated from the shared-bucket test run; this file is
// deliberately excluded from that shared bucket and run alongside App.test.tsx instead — see
// package.json's "test" script. Do not fold it back into the shared `find`-based run.

// Same reason App.test.tsx mocks this: rendering the real shell pulls in the real realtime hook,
// which would otherwise open a genuine SignalR connection during a test. bun's mock.module isn't
// hoisted the way Vitest's is, so it has to run before the component under test is imported.
const actualRealtime = await import('../operatorRealtime');
mock.module('../operatorRealtime', () => ({
  ...actualRealtime,
  createOperatorRealtimeClient: mock(() => ({
    start: mock(async () => {}),
    stop: mock(async () => {})
  }))
}));

const { App } = await import('../App');
const { writeSupportSession } = await import('./supportSession');

const originalFetch = globalThis.fetch;

// Broad, permissive stand-in for the platform API: the point of this test is proving the real
// operator-shell renders under a support session (not the sign-in form, not a placeholder screen),
// not exercising every workspace's data. Two shapes matter enough to get right (an object, not the
// catch-all array) so the hooks that consume them don't fail while mapping the response; everything
// else degrades to an empty list/loading state the same way it would for a staff session against a
// slow/empty backend — that degradation is already covered by this app's other tests.
const branchNamesById: Record<string, string> = {
  b1: 'Филиал на Рудаки',
  b2: 'Филиал на Айни'
};

async function supportShellFetch(input: RequestInfo | URL): Promise<Response> {
  const pathname = new URL(String(input)).pathname;

  if (pathname.endsWith('/floor-map')) {
    return jsonResponse({ branchId: 'b1', branchName: branchNamesById.b1, seats: [], zones: [], walls: [] });
  }
  const profileMatch = pathname.match(/\/branches\/([^/]+)\/profile$/);
  if (profileMatch) {
    return jsonResponse({ name: branchNamesById[profileMatch[1]] ?? '' });
  }
  return jsonResponse([]);
}

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

describe('App under an active support session', () => {
  beforeEach(() => {
    globalThis.fetch = mock(supportShellFetch) as unknown as typeof fetch;
  });

  afterEach(() => {
    cleanup();
    globalThis.fetch = originalFetch;
    sessionStorage.clear();
    localStorage.clear();
    delete window.__AFK4_ORGANIZATION_ADMIN_CONFIG__;
  });

  it('renders the real operator shell — not the sign-in form, not a placeholder screen', async () => {
    writeSupportSession({
      sessionToken: 'support-token-1',
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      organizationName: 'AFK4 Dushanbe',
      reason: 'Смена не открывается',
      expiresAtUtc: new Date(Date.now() + 5 * 60_000).toISOString(),
      writableAreas: ['branch-settings'],
      branches: [
        { branchId: 'b1', name: 'Филиал на Рудаки' },
        { branchId: 'b2', name: 'Филиал на Айни' }
      ]
    });

    render(<App />);

    // Only the real shell has this landmark — the sign-in form and any placeholder screen don't.
    expect(await screen.findByRole('navigation', { name: 'Рабочие места' })).toBeInTheDocument();
    expect(screen.queryByLabelText('Логин или email')).not.toBeInTheDocument();
    // The identity shown throughout the shell (rail, status bar, account panel) is the support
    // grant's own — proof the adapted session, not a staff one, is what's actually driving it.
    expect(screen.getAllByText(/Поддержка платформы/).length).toBeGreaterThan(0);
    // Two branches in the grant — the branch switcher (staff-only UI) must reflect the real,
    // per-branch names the backend returned for this organization, not be silently empty or
    // duplicated. Names load async (a per-branch profile fetch), hence findByText.
    expect((await screen.findAllByText('Филиал на Рудаки')).length).toBeGreaterThan(0);
    expect((await screen.findAllByText('Филиал на Айни')).length).toBeGreaterThan(0);
  });
});
