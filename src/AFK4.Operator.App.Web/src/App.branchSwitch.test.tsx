import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, mock, type Mock } from 'bun:test';
import type { OperatorRealtimeOptions, SessionLifecycleChangedDto } from './operatorRealtime';

// Task 11: реактивный контекст филиала + свитчер в шапке. Отдельный (не App.test.tsx) файл — тот
// уже 4800+ строк, здесь только сценарии свитчера/переподписки, со своим минимальным набором
// моков (не тем гигантским mockPlatformFetch).

type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

interface RealtimeInstance {
  options: OperatorRealtimeOptions;
  start: Mock<() => Promise<void>>;
  stop: Mock<() => Promise<void>>;
}

const realtimeInstances: RealtimeInstance[] = [];

// bun's mock.module is NOT hoisted above static imports, so it must be registered before the
// component under test is imported (same constraint as App.test.tsx).
const actualRealtime = await import('./operatorRealtime');
mock.module('./operatorRealtime', () => ({
  ...actualRealtime,
  createOperatorRealtimeClient: mock((options: OperatorRealtimeOptions) => {
    const start = mock(async () => {
      options.onConnectionStateChanged?.('connected');
    });
    const stop = mock(async () => {
      options.onConnectionStateChanged?.('disconnected');
    });
    realtimeInstances.push({ options, start, stop });
    return { start, stop };
  })
}));

const { App } = await import('./App');

const ORG_ID = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';
const BRANCH_A = 'acfc0212-967f-4d84-94be-9003387b09c2';
const BRANCH_B = 'bdbdbdbd-2222-3333-4444-555566667777';
const STAFF_ID = '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134';
const BRANCH_NAMES: Record<string, string> = {
  [BRANCH_A]: 'Центр',
  [BRANCH_B]: 'Север'
};

// One "ready" (free) seat per branch, distinctly named, so a test can prove the operator is
// looking at — and acting on — the branch that is actually active, not just that a request fired.
const SEAT_A = {
  seatId: '11111111-1111-1111-1111-111111111111',
  seatName: 'PC-A1',
  deviceId: 'aaaaaaaa-2222-2222-2222-222222222222'
};
const SEAT_B = {
  seatId: '22222222-2222-2222-2222-222222222222',
  seatName: 'PC-B1',
  deviceId: 'bbbbbbbb-2222-2222-2222-222222222222'
};
const SEAT_BY_BRANCH: Record<string, typeof SEAT_A> = {
  [BRANCH_A]: SEAT_A,
  [BRANCH_B]: SEAT_B
};

function floorMapSeatDto(seat: typeof SEAT_A) {
  return {
    seatId: seat.seatId,
    seatName: seat.seatName,
    zoneId: 'aaaaaaaa-0000-0000-0000-000000000001',
    zoneName: 'Зал',
    sortOrder: 1,
    // 'Locked' (not yet occupied) resolves to tone 'ready' — the "+"-tile that opens Start.
    state: 'Locked',
    deviceId: seat.deviceId,
    deviceName: seat.seatName,
    isDeviceOnline: true,
    isDeviceLocked: true,
    activeSessionId: null,
    remainingSeconds: null
  };
}

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

function createSession(overrides: Record<string, unknown> = {}) {
  return {
    staffUserId: STAFF_ID,
    organizationId: ORG_ID,
    displayName: 'Cashier One',
    accessToken: 'access-token',
    // Far in the future so the auth hook never bothers with a silent refresh — keeps the mock
    // fetch surface minimal.
    accessTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
    refreshToken: 'refresh-token',
    refreshTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
    branchIds: [BRANCH_A, BRANCH_B],
    activeBranchId: BRANCH_A,
    // Only floor_map.view: keeps the shift gate ('not-required') and the players preload
    // (permission-gated) out of the picture, so this file doesn't need their endpoints mocked.
    permissions: ['organization.floor_map.view'],
    ...overrides
  };
}

function mockFetch(input: RequestInfo | URL): Promise<Response> {
  const pathname = new URL(String(input)).pathname;
  const branchMatch = pathname.match(/\/api\/branches\/([^/]+)\//);
  const branchId = branchMatch?.[1] ?? BRANCH_A;

  if (pathname.endsWith('/floor-map')) {
    return Promise.resolve(jsonResponse({ branchId, branchName: BRANCH_NAMES[branchId] ?? branchId, seats: [] }));
  }

  if (pathname.endsWith('/profile')) {
    return Promise.resolve(jsonResponse({
      organizationId: ORG_ID,
      branchId,
      name: BRANCH_NAMES[branchId] ?? branchId,
      city: 'Dushanbe'
    }));
  }

  return Promise.resolve(new Response('not found', { status: 404 }));
}

const originalFetch = globalThis.fetch;
let fetchMock: Mock<FetchLike>;

function installConfig() {
  window.__AFK4_OPERATOR_CONFIG__ = {
    runtime: 'browser-dev',
    shellMode: 'vite-dev',
    platformBaseUrl: 'http://localhost:5074/',
    currencyCode: 'TJS',
    organizationId: ORG_ID,
    branchId: BRANCH_A
  };
}

describe('App — branch switch (Task 11)', () => {
  beforeEach(() => {
    realtimeInstances.length = 0;
    fetchMock = mock(mockFetch) as unknown as Mock<FetchLike>;
    globalThis.fetch = fetchMock as unknown as typeof fetch;
  });

  afterEach(() => {
    cleanup();
    globalThis.fetch = originalFetch;
    delete window.__AFK4_OPERATOR_CONFIG__;
    localStorage.clear();
    sessionStorage.clear();
    mock.restore();
  });

  it('hides the branch switcher for a single-branch session', async () => {
    installConfig();
    sessionStorage.setItem('afk4.staff.session', JSON.stringify(createSession({
      branchIds: [BRANCH_A],
      activeBranchId: BRANCH_A
    })));

    render(<App />);

    await screen.findByRole('navigation', { name: 'Рабочие места' });
    expect(screen.queryByRole('group', { name: 'Филиалы' })).not.toBeInTheDocument();
  });

  it('shows the branch switcher for a multi-branch session and switches the active branch', async () => {
    installConfig();
    sessionStorage.setItem('afk4.staff.session', JSON.stringify(createSession()));

    render(<App />);

    await screen.findByRole('navigation', { name: 'Рабочие места' });
    const switcher = await screen.findByRole('group', { name: 'Филиалы' });
    await within(switcher).findByText('Центр');
    await within(switcher).findByText('Север');

    expect(within(switcher).getByText('Центр').closest('button')).toHaveAttribute('aria-current', 'true');
    expect(within(switcher).getByText('Север').closest('button')).not.toHaveAttribute('aria-current');

    fireEvent.click(within(switcher).getByText('Север'));

    expect(within(switcher).getByText('Север').closest('button')).toHaveAttribute('aria-current', 'true');
    expect(within(switcher).getByText('Центр').closest('button')).not.toHaveAttribute('aria-current');
  });

  it('persists the switched branch across a remount (localStorage)', async () => {
    installConfig();
    sessionStorage.setItem('afk4.staff.session', JSON.stringify(createSession()));

    const { unmount } = render(<App />);
    const switcher = await screen.findByRole('group', { name: 'Филиалы' });
    await within(switcher).findByText('Север');
    fireEvent.click(within(switcher).getByText('Север'));
    await waitFor(() => expect(within(switcher).getByText('Север').closest('button')?.getAttribute('aria-current'))
      .toBe('true'));
    unmount();

    render(<App />);
    const remountedSwitcher = await screen.findByRole('group', { name: 'Филиалы' });
    await within(remountedSwitcher).findByText('Север');
    expect(within(remountedSwitcher).getByText('Север').closest('button')?.getAttribute('aria-current')).toBe('true');
  });

  it('tears down the old realtime subscription and rebinds scope matching to the new branch on switch', async () => {
    installConfig();
    sessionStorage.setItem('afk4.staff.session', JSON.stringify(createSession()));

    let floorMapRequestCount = 0;
    fetchMock.mockImplementation((input) => {
      if (new URL(String(input)).pathname.endsWith('/floor-map')) {
        floorMapRequestCount += 1;
      }
      return mockFetch(input);
    });

    render(<App />);

    const switcher = await screen.findByRole('group', { name: 'Филиалы' });
    await within(switcher).findByText('Север');
    await waitFor(() => expect(realtimeInstances).toHaveLength(1));

    const lifecycleEvent = (branchId: string): SessionLifecycleChangedDto => ({
      organizationId: ORG_ID,
      branchId,
      seatId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      sessionId: '22222222-2222-2222-2222-222222222222',
      kind: 'ended',
      state: 'ending',
      version: 1,
      startedAtUtc: '2026-05-21T09:00:00Z',
      endsAtUtc: null,
      observedAtUtc: '2026-05-21T10:00:00Z'
    });

    // Before the switch, an event scoped to branch B (not yet active) is ignored by the first
    // subscription — proves the scope check is live, not a no-op, before we even touch the switch.
    const beforeSwitch = floorMapRequestCount;
    act(() => {
      realtimeInstances[0].options.onSessionLifecycleChanged?.(lifecycleEvent(BRANCH_B));
    });
    await new Promise((resolve) => setTimeout(resolve, 150));
    expect(floorMapRequestCount).toBe(beforeSwitch);

    fireEvent.click(within(switcher).getByText('Север'));

    // Clean teardown of the old subscription + a fresh one opened for the new branch.
    await waitFor(() => expect(realtimeInstances).toHaveLength(2));
    expect(realtimeInstances[0].stop).toHaveBeenCalledTimes(1);
    expect(realtimeInstances[1].start).toHaveBeenCalledTimes(1);

    // An event scoped to the now-inactive branch A is ignored on the new subscription...
    const beforeStaleEvent = floorMapRequestCount;
    act(() => {
      realtimeInstances[1].options.onSessionLifecycleChanged?.(lifecycleEvent(BRANCH_A));
    });
    await new Promise((resolve) => setTimeout(resolve, 150));
    expect(floorMapRequestCount).toBe(beforeStaleEvent);

    // ...while one scoped to the newly-active branch B reconciles the floor map — the realtime
    // channel's scope genuinely moved to the new branch, not just the UI label.
    act(() => {
      realtimeInstances[1].options.onSessionLifecycleChanged?.(lifecycleEvent(BRANCH_B));
    });
    await waitFor(() => expect(floorMapRequestCount).toBeGreaterThan(beforeStaleEvent));
  });

  // Review fix: useFloorMap/useShellData/usePlayersPreload used to resolve the branch with the
  // frozen 2-arg resolveActiveBranchId(authSession, config.branchId), which always resolves to
  // session.activeBranchId (here BRANCH_A) — the switcher's chosenBranchId never fed in. These
  // three tests exercise the reactive activeBranchId prop end to end: real HTTP requests, real
  // branchId values in the URL, no mocked-away branch resolution.

  it('reloads the floor map for the newly active branch, not the branch frozen on the session', async () => {
    installConfig();
    // session.activeBranchId stays pinned to A for the whole test — exactly the value the old
    // 2-arg resolveActiveBranchId kept returning after the switch.
    sessionStorage.setItem('afk4.staff.session', JSON.stringify(createSession()));

    const floorMapPaths: string[] = [];
    fetchMock.mockImplementation((input) => {
      const pathname = new URL(String(input)).pathname;
      if (pathname.endsWith('/floor-map')) {
        floorMapPaths.push(pathname);
      }
      return mockFetch(input);
    });

    render(<App />);

    const switcher = await screen.findByRole('group', { name: 'Филиалы' });
    await within(switcher).findByText('Север');
    await waitFor(() => expect(floorMapPaths.some((path) => path.includes(BRANCH_A))).toBe(true));

    fireEvent.click(within(switcher).getByText('Север'));

    await waitFor(() => expect(floorMapPaths.some((path) => path.includes(BRANCH_B))).toBe(true));
  });

  it('scopes a seat action (session start) to the newly active branch after switch', async () => {
    installConfig();
    sessionStorage.setItem('afk4.staff.session', JSON.stringify(createSession({
      permissions: ['organization.floor_map.view', 'organization.sessions.start']
    })));

    const startCalls: string[] = [];
    fetchMock.mockImplementation((input, init) => {
      const pathname = new URL(String(input)).pathname;
      const branchMatch = pathname.match(/\/api\/branches\/([^/]+)\//);
      const branchId = branchMatch?.[1] ?? BRANCH_A;

      if (pathname.endsWith('/floor-map')) {
        const seat = SEAT_BY_BRANCH[branchId];
        return Promise.resolve(jsonResponse({
          branchId,
          branchName: BRANCH_NAMES[branchId] ?? branchId,
          seats: seat ? [floorMapSeatDto(seat)] : []
        }));
      }
      if (pathname.endsWith('/sessions/start') && init?.method === 'POST') {
        startCalls.push(branchId);
        return Promise.resolve(jsonResponse({}));
      }
      return mockFetch(input);
    });

    render(<App />);

    const switcher = await screen.findByRole('group', { name: 'Филиалы' });
    await within(switcher).findByText('Север');
    fireEvent.click(within(switcher).getByText('Север'));

    // The reloaded map now shows branch B's seat — proves the map itself moved before we even act.
    const startTile = await screen.findByRole('button', { name: /PC-B1/ });
    fireEvent.click(startTile);

    const startDialog = await screen.findByRole('dialog', { name: 'Новая сессия' });
    const confirmButton = await within(startDialog).findByRole('button', { name: /Старт · открытый счёт/ });
    fireEvent.click(confirmButton);

    await waitFor(() => expect(startCalls.length).toBeGreaterThan(0));
    expect(startCalls).toEqual([BRANCH_B]);
  });

  it('refetches shell shift data for the newly active branch after switch', async () => {
    installConfig();
    sessionStorage.setItem('afk4.staff.session', JSON.stringify(createSession({
      permissions: ['organization.floor_map.view', 'organization.shifts.view']
    })));

    const shiftPaths: string[] = [];
    fetchMock.mockImplementation((input) => {
      const pathname = new URL(String(input)).pathname;
      if (pathname.endsWith('/shifts/current')) {
        shiftPaths.push(pathname);
        return Promise.resolve(jsonResponse({ state: 'open', openedAtUtc: '2026-05-21T08:00:00Z' }));
      }
      return mockFetch(input);
    });

    render(<App />);

    const switcher = await screen.findByRole('group', { name: 'Филиалы' });
    await within(switcher).findByText('Север');
    await waitFor(() => expect(shiftPaths.some((path) => path.includes(BRANCH_A))).toBe(true));

    fireEvent.click(within(switcher).getByText('Север'));

    await waitFor(() => expect(shiftPaths.some((path) => path.includes(BRANCH_B))).toBe(true));
  });
});
