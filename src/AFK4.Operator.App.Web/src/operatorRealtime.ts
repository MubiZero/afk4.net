import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';

export type OperatorRealtimeConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface DeviceStatusChangedDto {
  organizationId: string;
  branchId: string;
  deviceId: string;
  machineName: string;
  isOnline: boolean;
  isLocked: boolean;
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
  onConnectionStateChanged?: (state: OperatorRealtimeConnectionState) => void;
  connectionFactory?: () => SignalRConnectionLike;
}

export const deviceStatusChangedEventName = 'deviceStatusChanged';

export function createOperatorRealtimeClient(options: OperatorRealtimeOptions): OperatorRealtimeClient {
  const connection = options.connectionFactory?.() ?? createSignalRConnection(options);
  const setState = (state: OperatorRealtimeConnectionState) => options.onConnectionStateChanged?.(state);

  connection.on<DeviceStatusChangedDto>(deviceStatusChangedEventName, options.onDeviceStatusChanged);
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
