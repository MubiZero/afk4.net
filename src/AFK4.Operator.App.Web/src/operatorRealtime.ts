import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import type { ShopOrderDto } from './operatorApiClients';

export type OperatorRealtimeConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface DeviceStatusChangedDto {
  organizationId: string;
  branchId: string;
  deviceId: string;
  machineName: string;
  isOnline: boolean;
  isLocked: boolean;
  observedAtUtc: string;
  displayName?: string;
  role?: string;
  enrollmentState?: string;
  seatId?: string | null;
}

export interface DeviceCommandResultDto {
  organizationId: string;
  branchId: string;
  deviceId: string;
  commandId: string;
  status: string;
  message: string;
  observedAtUtc: string;
}

export interface SessionLifecycleChangedDto {
  organizationId: string;
  branchId: string;
  seatId: string;
  sessionId: string;
  kind: string;
  state: string;
  version: number;
  startedAtUtc?: string | null;
  endsAtUtc?: string | null;
  observedAtUtc: string;
  accruedCostMinorUnits?: number | null;
  currencyCode?: string | null;
}

export interface OperatorRealtimeClient {
  start(): Promise<void>;
  stop(): Promise<void>;
}

export interface SignalRConnectionLike {
  state: HubConnectionState | string;
  on<TPayload>(methodName: string, newMethod: (payload: TPayload) => void): void;
  onclose(callback: (error?: Error) => void): void;
  onreconnected(callback: (connectionId?: string) => void): void;
  onreconnecting(callback: (error?: Error) => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
}

export interface OperatorRealtimeOptions {
  baseUrl: string;
  getAccessToken: () => string | null | Promise<string | null>;
  onDeviceStatusChanged: (status: DeviceStatusChangedDto) => void;
  onDeviceCommandResult?: (result: DeviceCommandResultDto) => void;
  onSessionLifecycleChanged?: (change: SessionLifecycleChangedDto) => void;
  onShopOrderCreated?: (order: ShopOrderDto) => void;
  onShopOrderUpdated?: (order: ShopOrderDto) => void;
  onConnectionStateChanged?: (state: OperatorRealtimeConnectionState) => void;
  connectionFactory?: () => SignalRConnectionLike;
}

export const deviceStatusChangedEventName = 'deviceStatusChanged';
export const deviceCommandResultEventName = 'deviceCommandResult';
export const sessionLifecycleChangedEventName = 'sessionLifecycleChanged';
export const shopOrderCreatedEventName = 'shopOrderCreated';
export const shopOrderUpdatedEventName = 'shopOrderUpdated';

export function createOperatorRealtimeClient(options: OperatorRealtimeOptions): OperatorRealtimeClient {
  const connection = options.connectionFactory?.() ?? createSignalRConnection(options);
  const setState = (state: OperatorRealtimeConnectionState) => options.onConnectionStateChanged?.(state);

  connection.on<DeviceStatusChangedDto>(deviceStatusChangedEventName, options.onDeviceStatusChanged);
  if (options.onDeviceCommandResult) {
    connection.on<DeviceCommandResultDto>(deviceCommandResultEventName, options.onDeviceCommandResult);
  }
  if (options.onSessionLifecycleChanged) {
    connection.on<SessionLifecycleChangedDto>(sessionLifecycleChangedEventName, options.onSessionLifecycleChanged);
  }
  if (options.onShopOrderCreated) {
    connection.on<ShopOrderDto>(shopOrderCreatedEventName, options.onShopOrderCreated);
  }
  if (options.onShopOrderUpdated) {
    connection.on<ShopOrderDto>(shopOrderUpdatedEventName, options.onShopOrderUpdated);
  }
  connection.onreconnecting(() => setState('reconnecting'));
  connection.onreconnected(() => setState('connected'));
  connection.onclose(() => setState('disconnected'));

  return {
    async start() {
      if (connection.state === HubConnectionState.Connected || connection.state === 'Connected') {
        setState('connected');
        return;
      }

      setState('connecting');
      await connection.start();
      setState('connected');
    },
    async stop() {
      await connection.stop();
      setState('disconnected');
    }
  };
}

// Vite preview uses fixture data rather than a platform hub. Keep its connection
// indicator truthful without opening a failing WebSocket negotiation locally.
export function createPreviewOperatorRealtimeClient(options: OperatorRealtimeOptions): OperatorRealtimeClient {
  return {
    async start() {
      options.onConnectionStateChanged?.('connecting');
      options.onConnectionStateChanged?.('connected');
    },
    async stop() {
      options.onConnectionStateChanged?.('disconnected');
    }
  };
}

export function buildDeviceHubUrl(baseUrl: string): string {
  return new URL('/hubs/devices', baseUrl).toString();
}

function createSignalRConnection(options: OperatorRealtimeOptions): SignalRConnectionLike {
  return new HubConnectionBuilder()
    .withUrl(buildDeviceHubUrl(options.baseUrl), {
      accessTokenFactory: async () => await options.getAccessToken() ?? ''
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
