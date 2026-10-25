import { matchesReservationScope } from './operatorHelpers';
import { describe, expect, it } from 'bun:test';
import {
  deviceCommandResultEventName,
  deviceStatusChangedEventName,
  reservationChangedEventName,
  sessionLifecycleChangedEventName,
  shopOrderCreatedEventName,
  shopOrderUpdatedEventName,
  type DeviceCommandResultDto,
  type DeviceStatusChangedDto,
  type SessionLifecycleChangedDto,
  type SignalRConnectionLike,
  realtimeRetryDelayMs
} from './operatorRealtime';

// App.test.tsx registers a process-wide mock.module('./operatorRealtime', ...) that bun
// cannot reliably restore for sibling files, so a plain import of the client factory here
// can resolve to App's stub when the whole suite runs in one process. The shared preload
// (src/test/setup.ts) captures the genuine module before any mock is installed; read the
// real implementation from there so these behavioural assertions exercise the real client.
const { buildDeviceHubUrl, createOperatorRealtimeClient, createPreviewOperatorRealtimeClient } = (
  globalThis as typeof globalThis & {
    __afk4RealOperatorRealtime: typeof import('./operatorRealtime');
  }
).__afk4RealOperatorRealtime;

describe('operator realtime client', () => {
  it('keeps browser preview connected without constructing a SignalR transport', async () => {
    const states: string[] = [];
    const client = createPreviewOperatorRealtimeClient({
      baseUrl: 'http://127.0.0.1:5174/',
      getAccessToken: () => 'preview-token',
      onDeviceStatusChanged: () => {},
      onConnectionStateChanged: (state) => states.push(state)
    });

    await client.start();
    await client.stop();

    expect(states).toEqual(['connecting', 'connected', 'disconnected']);
  });

  it('targets the ASP.NET Core device hub path from any platform base URL', () => {
    expect(buildDeviceHubUrl('http://localhost:5074/platform/')).toBe('http://localhost:5074/hubs/devices');
  });

  it('registers every hub event even when a surface does not consume its payload', () => {
    const connection = new FakeSignalRConnection();
    createOperatorRealtimeClient({
      baseUrl: 'http://localhost:5074/',
      getAccessToken: () => 'access-token',
      connectionFactory: () => connection,
      onDeviceStatusChanged: () => {}
    });

    expect(connection.registeredEvents()).toEqual([
      deviceCommandResultEventName,
      deviceStatusChangedEventName,
      reservationChangedEventName,
      sessionLifecycleChangedEventName,
      shopOrderCreatedEventName,
      shopOrderUpdatedEventName
    ]);
  });

  it('wires device status events and connection state transitions', async () => {
    const connection = new FakeSignalRConnection();
    const states: string[] = [];
    const statuses: DeviceStatusChangedDto[] = [];
    const results: DeviceCommandResultDto[] = [];
    const lifecycles: SessionLifecycleChangedDto[] = [];
    const client = createOperatorRealtimeClient({
      baseUrl: 'http://localhost:5074/',
      getAccessToken: () => 'access-token',
      connectionFactory: () => connection,
      onConnectionStateChanged: (state) => states.push(state),
      onDeviceStatusChanged: (status) => statuses.push(status),
      onDeviceCommandResult: (result) => results.push(result),
      onSessionLifecycleChanged: (change) => lifecycles.push(change)
    });

    await client.start();
    connection.emit(sessionLifecycleChangedEventName, {
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
      seatId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      sessionId: '22222222-2222-2222-2222-222222222222',
      kind: 'started',
      state: 'active',
      version: 1,
      startedAtUtc: '2026-05-21T10:00:00Z',
      endsAtUtc: null,
      observedAtUtc: '2026-05-21T10:00:00Z'
    });
    connection.emit(deviceStatusChangedEventName, {
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
      deviceId: '11111111-1111-1111-1111-111111111111',
      machineName: 'PC-01',
      isOnline: true,
      isLocked: false,
      observedAtUtc: '2026-05-21T10:00:00Z'
    });
    connection.emit(deviceCommandResultEventName, {
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
      deviceId: '11111111-1111-1111-1111-111111111111',
      commandId: '44444444-4444-4444-4444-444444444444',
      status: 'accepted',
      message: 'Agent accepted lock',
      observedAtUtc: '2026-05-21T10:00:01Z'
    });
    connection.reconnect();
    await client.stop();

    expect(states).toEqual(['connecting', 'connected', 'reconnecting', 'connected', 'disconnected', 'disconnected']);
    expect(statuses).toHaveLength(1);
    expect(statuses[0]).toMatchObject({
      machineName: 'PC-01',
      isLocked: false
    });
    expect(results).toHaveLength(1);
    expect(results[0]).toMatchObject({
      commandId: '44444444-4444-4444-4444-444444444444',
      status: 'accepted'
    });
    expect(lifecycles).toHaveLength(1);
    expect(lifecycles[0]).toMatchObject({
      kind: 'started',
      state: 'active',
      version: 1
    });
  });
});

describe('operator realtime shop events', () => {
  it('routes shopOrderCreated to the handler', () => {
    const connection = new FakeSignalRConnection();
    let received: unknown;
    createOperatorRealtimeClient({
      baseUrl: 'http://localhost:5074/',
      getAccessToken: () => 'access-token',
      connectionFactory: () => connection,
      onDeviceStatusChanged: () => {},
      onShopOrderCreated: (order) => { received = order; }
    });
    connection.emit(shopOrderCreatedEventName, { id: 'o1' });
    expect(received).toEqual({ id: 'o1' });
  });

  it('routes shopOrderUpdated to the handler', () => {
    const connection = new FakeSignalRConnection();
    let received: unknown;
    createOperatorRealtimeClient({
      baseUrl: 'http://localhost:5074/',
      getAccessToken: () => 'access-token',
      connectionFactory: () => connection,
      onDeviceStatusChanged: () => {},
      onShopOrderUpdated: (order) => { received = order; }
    });
    connection.emit(shopOrderUpdatedEventName, { id: 'o2', status: 'accepted' });
    expect(received).toEqual({ id: 'o2', status: 'accepted' });
  });
});

describe('operator realtime reconnection', () => {
  const manualScheduler = () => {
    const pending: { retry: () => void; delayMs: number; cancelled: boolean }[] = [];
    return {
      pending,
      schedule: (retry: () => void, delayMs: number) => {
        const entry = { retry, delayMs, cancelled: false };
        pending.push(entry);
        return () => { entry.cancelled = true; };
      },
      async runNext() {
        const entry = pending.shift();
        if (entry && !entry.cancelled) {
          entry.retry();
          await new Promise((resolve) => setTimeout(resolve, 0));
        }
      }
    };
  };

  it('keeps retrying a first start that fails while the club is offline, then connects', async () => {
    const connection = new FakeSignalRConnection();
    connection.failStarts = 2;
    const scheduler = manualScheduler();
    const states: string[] = [];
    const errors: unknown[] = [];
    const client = createOperatorRealtimeClient({
      baseUrl: 'http://localhost:5074/',
      getAccessToken: () => 'access-token',
      connectionFactory: () => connection,
      scheduleRetry: scheduler.schedule,
      onConnectionStateChanged: (state) => states.push(state),
      onConnectionError: (error) => errors.push(error),
      onDeviceStatusChanged: () => {}
    });

    await client.start();
    expect(states.at(-1)).toBe('reconnecting');
    await scheduler.runNext();
    await scheduler.runNext();

    expect(connection.startCalls).toBe(3);
    expect(errors).toHaveLength(2);
    expect(states.at(-1)).toBe('connected');
  });

  it('reconnects on its own after the hub closes the connection', async () => {
    const connection = new FakeSignalRConnection();
    const scheduler = manualScheduler();
    const states: string[] = [];
    const client = createOperatorRealtimeClient({
      baseUrl: 'http://localhost:5074/',
      getAccessToken: () => 'access-token',
      connectionFactory: () => connection,
      scheduleRetry: scheduler.schedule,
      onConnectionStateChanged: (state) => states.push(state),
      onDeviceStatusChanged: () => {}
    });
    await client.start();

    connection.serverClose();
    expect(states.at(-1)).toBe('reconnecting');
    await scheduler.runNext();

    expect(connection.startCalls).toBe(2);
    expect(states.at(-1)).toBe('connected');
  });

  it('stops for good when the desk closes it, even with a retry pending', async () => {
    const connection = new FakeSignalRConnection();
    connection.failStarts = 1;
    const scheduler = manualScheduler();
    const client = createOperatorRealtimeClient({
      baseUrl: 'http://localhost:5074/',
      getAccessToken: () => 'access-token',
      connectionFactory: () => connection,
      scheduleRetry: scheduler.schedule,
      onDeviceStatusChanged: () => {}
    });
    await client.start();

    await client.stop();
    await scheduler.runNext();

    expect(connection.startCalls).toBe(1);
    expect(scheduler.pending).toHaveLength(0);
  });

  it('backs off up to thirty seconds with up to a second of jitter', () => {
    expect(realtimeRetryDelayMs(0, () => 0)).toBe(1_000);
    expect(realtimeRetryDelayMs(3, () => 0)).toBe(8_000);
    expect(realtimeRetryDelayMs(50, () => 0)).toBe(30_000);
    expect(realtimeRetryDelayMs(50, () => 0.999)).toBe(30_999);
  });
});

class FakeSignalRConnection implements SignalRConnectionLike {
  state = 'Disconnected';
  private readonly handlers = new Map<string, (payload: unknown) => void>();
  private closeHandler: ((error?: Error) => void) | null = null;
  private reconnectedHandler: ((connectionId?: string) => void) | null = null;
  private reconnectingHandler: ((error?: Error) => void) | null = null;

  on<TPayload>(methodName: string, newMethod: (payload: TPayload) => void): void {
    this.handlers.set(methodName, newMethod as (payload: unknown) => void);
  }

  onclose(callback: (error?: Error) => void): void {
    this.closeHandler = callback;
  }

  onreconnected(callback: (connectionId?: string) => void): void {
    this.reconnectedHandler = callback;
  }

  onreconnecting(callback: (error?: Error) => void): void {
    this.reconnectingHandler = callback;
  }

  failStarts = 0;
  startCalls = 0;

  async start(): Promise<void> {
    this.startCalls += 1;
    if (this.failStarts > 0) {
      this.failStarts -= 1;
      throw new Error('network down');
    }
    this.state = 'Connected';
  }

  /** Хаб закрыл соединение сам (обрыв дольше, чем выдерживает встроенный автореконнект). */
  serverClose(): void {
    this.state = 'Disconnected';
    this.closeHandler?.(new Error('connection lost'));
  }

  async stop(): Promise<void> {
    this.state = 'Disconnected';
    this.closeHandler?.();
  }

  emit(methodName: string, payload: unknown): void {
    this.handlers.get(methodName)?.(payload);
  }

  registeredEvents(): string[] {
    return [...this.handlers.keys()].sort();
  }

  reconnect(): void {
    this.reconnectingHandler?.();
    this.reconnectedHandler?.('connection-1');
  }
}

// Хаб вещает всё, что происходит в организации, а администратор смотрит на один филиал. Чужая
// заявка, дёрнувшая его полосу, читается как «кто-то трогает мои брони» — и это хуже, чем
// секундная задержка, ради которой всё затевалось.
it('reservation change from another branch is not this desk’s business', () => {
  const session = { organizationId: 'ORG-1' } as Parameters<typeof matchesReservationScope>[1];
  const change = (branchId: string) => ({
    organizationId: 'org-1', branchId, reservationId: 'r1', seatId: null,
    kind: 'rejected', state: 'rejected', version: 2,
    startsAtUtc: '2026-09-20T19:00:00Z', observedAtUtc: '2026-09-20T17:00:00Z'
  });

  expect(matchesReservationScope(change('branch-a'), session, 'BRANCH-A')).toBe(true);
  expect(matchesReservationScope(change('branch-b'), session, 'branch-a')).toBe(false);
});
