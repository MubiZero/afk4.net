import { describe, expect, it } from 'bun:test';
import {
  deviceCommandResultEventName,
  deviceStatusChangedEventName,
  sessionLifecycleChangedEventName,
  shopOrderCreatedEventName,
  shopOrderUpdatedEventName,
  type DeviceCommandResultDto,
  type DeviceStatusChangedDto,
  type SessionLifecycleChangedDto,
  type SignalRConnectionLike
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

  async start(): Promise<void> {
    this.state = 'Connected';
  }

  async stop(): Promise<void> {
    this.state = 'Disconnected';
    this.closeHandler?.();
  }

  emit(methodName: string, payload: unknown): void {
    this.handlers.get(methodName)?.(payload);
  }

  reconnect(): void {
    this.reconnectingHandler?.();
    this.reconnectedHandler?.('connection-1');
  }
}
