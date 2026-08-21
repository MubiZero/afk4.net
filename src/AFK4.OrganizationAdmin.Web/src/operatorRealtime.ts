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

/**
 * Что случилось с бронью. Полоса заявок до сих пор узнавала об этом следующим опросом: чужое
 * решение доезжало через несколько секунд, а решения таймеров — истёкший срок ответа — не
 * доезжали вовсе, заявка просто исчезала.
 */
export interface ReservationChangedDto {
  organizationId: string;
  branchId: string;
  reservationId: string;
  seatId?: string | null;
  kind: string;
  state: string;
  version: number;
  startsAtUtc: string;
  observedAtUtc: string;
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
  onReservationChanged?: (change: ReservationChangedDto) => void;
  onShopOrderCreated?: (order: ShopOrderDto) => void;
  onShopOrderUpdated?: (order: ShopOrderDto) => void;
  onConnectionStateChanged?: (state: OperatorRealtimeConnectionState) => void;
  connectionFactory?: () => SignalRConnectionLike;
}

export const deviceStatusChangedEventName = 'deviceStatusChanged';
export const deviceCommandResultEventName = 'deviceCommandResult';
export const sessionLifecycleChangedEventName = 'sessionLifecycleChanged';
export const reservationChangedEventName = 'reservationChanged';
export const shopOrderCreatedEventName = 'shopOrderCreated';
export const shopOrderUpdatedEventName = 'shopOrderUpdated';

const ignoreRealtimeEvent = (): void => {};

export function createOperatorRealtimeClient(options: OperatorRealtimeOptions): OperatorRealtimeClient {
  const connection = options.connectionFactory?.() ?? createSignalRConnection(options);
  const setState = (state: OperatorRealtimeConnectionState) => options.onConnectionStateChanged?.(state);

  connection.on<DeviceStatusChangedDto>(deviceStatusChangedEventName, options.onDeviceStatusChanged);
  // The device hub broadcasts its complete event surface to every authenticated connection.
  // Some Operator surfaces consume only a subset (for example the shop-order ticker), but
  // SignalR logs a warning for every server invocation without a registered client method.
  // Keep no-op handlers for unconsumed events so parallel surface connections stay quiet while
  // the owning surface still receives the callbacks it requested.
  connection.on<DeviceCommandResultDto>(deviceCommandResultEventName, options.onDeviceCommandResult ?? ignoreRealtimeEvent);
  connection.on<SessionLifecycleChangedDto>(sessionLifecycleChangedEventName, options.onSessionLifecycleChanged ?? ignoreRealtimeEvent);
  connection.on<ReservationChangedDto>(reservationChangedEventName, options.onReservationChanged ?? ignoreRealtimeEvent);
  connection.on<ShopOrderDto>(shopOrderCreatedEventName, options.onShopOrderCreated ?? ignoreRealtimeEvent);
  connection.on<ShopOrderDto>(shopOrderUpdatedEventName, options.onShopOrderUpdated ?? ignoreRealtimeEvent);
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
