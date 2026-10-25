import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import type { ShopOrderDto } from './operatorApiClients';
import type {
  DeviceCommandResultDto,
  DeviceStatusChangedDto,
  ReservationChangedDto,
  SessionLifecycleChangedDto,
} from '@afk4/contracts';
export type {
  DeviceCommandResultDto,
  DeviceStatusChangedDto,
  ReservationChangedDto,
  SessionLifecycleChangedDto,
} from '@afk4/contracts';

export type OperatorRealtimeConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

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
  /** Попытка подключиться не удалась; клиент сам повторит её позже. */
  onConnectionError?: (error: unknown) => void;
  connectionFactory?: () => SignalRConnectionLike;
  /** Таймер повторов; в тестах подменяется, чтобы не ждать настоящих секунд. */
  scheduleRetry?: (retry: () => void, delayMs: number) => () => void;
}

export const deviceStatusChangedEventName = 'deviceStatusChanged';
export const deviceCommandResultEventName = 'deviceCommandResult';
export const sessionLifecycleChangedEventName = 'sessionLifecycleChanged';
export const reservationChangedEventName = 'reservationChanged';
export const shopOrderCreatedEventName = 'shopOrderCreated';
export const shopOrderUpdatedEventName = 'shopOrderUpdated';
/** Событие окна: учебный бэкенд сообщает, что команда ПК выполнена (только превью и демо). */
export const previewDeviceCommandResultEventName = 'afk4:preview-device-command-result';

const ignoreRealtimeEvent = (): void => {};

/**
 * Пауза перед следующей попыткой: 1, 2, 4, 8, 16, 30 секунд и дальше по 30, плюс до секунды
 * случайно, чтобы десяток касс после сбоя сети не стучались в хаб одновременно. Попытки не
 * кончаются: интернет в клубе может пропасть на полчаса, а Панель должна вернуться к живым данным сама.
 */
export function realtimeRetryDelayMs(attempt: number, random: () => number = Math.random): number {
  return Math.min(30_000, 1_000 * 2 ** Math.min(Math.max(attempt, 0), 5)) + Math.floor(random() * 1_000);
}

const scheduleWithTimer = (retry: () => void, delayMs: number): (() => void) => {
  const handle = setTimeout(retry, delayMs);
  return () => clearTimeout(handle);
};

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
  const scheduleRetry = options.scheduleRetry ?? scheduleWithTimer;
  let stopped = false;
  let cancelRetry: (() => void) | null = null;

  // Встроенный автореконнект SignalR после сбоя на старте не работает вовсе, а закрытое соединение
  // не поднимает: касса оставалась «без связи» до перезагрузки страницы. Здесь любая неудача
  // (первый старт без сети, закрытие после долгого обрыва) ведёт к новой попытке по расписанию.
  const connect = async (attempt: number): Promise<void> => {
    cancelRetry = null;
    if (stopped) {
      return;
    }

    setState(attempt === 0 ? 'connecting' : 'reconnecting');
    try {
      await connection.start();
      if (!stopped) {
        setState('connected');
      }
    } catch (error) {
      if (stopped) {
        return;
      }

      options.onConnectionError?.(error);
      setState('reconnecting');
      cancelRetry = scheduleRetry(() => void connect(attempt + 1), realtimeRetryDelayMs(attempt));
    }
  };

  connection.onreconnecting(() => setState('reconnecting'));
  connection.onreconnected(() => setState('connected'));
  connection.onclose(() => {
    if (stopped) {
      setState('disconnected');
      return;
    }

    setState('reconnecting');
    cancelRetry = scheduleRetry(() => void connect(1), realtimeRetryDelayMs(0));
  });

  return {
    async start() {
      stopped = false;
      if (connection.state === HubConnectionState.Connected || connection.state === 'Connected') {
        setState('connected');
        return;
      }

      await connect(0);
    },
    async stop() {
      stopped = true;
      cancelRetry?.();
      cancelRetry = null;
      await connection.stop();
      setState('disconnected');
    }
  };
}

// Vite preview uses fixture data rather than a platform hub. Keep its connection
// indicator truthful without opening a failing WebSocket negotiation locally.
export function createPreviewOperatorRealtimeClient(options: OperatorRealtimeOptions): OperatorRealtimeClient {
  // ПК в превью и в демо нет, но учебный бэкенд страницы исполняет команды сам и сообщает результат
  // так же, как хаб: карта перечитывается, и «Разбудить» или «Вернуть в зал» видны на месте.
  const onCommandResult = (event: Event) => options.onDeviceCommandResult?.((event as CustomEvent<DeviceCommandResultDto>).detail);
  return {
    async start() {
      options.onConnectionStateChanged?.('connecting');
      window.addEventListener(previewDeviceCommandResultEventName, onCommandResult);
      options.onConnectionStateChanged?.('connected');
    },
    async stop() {
      window.removeEventListener(previewDeviceCommandResultEventName, onCommandResult);
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
    // Повторы без предела (тот же график, что и после закрытия) и докачка пропущенных сообщений
    // после короткого обрыва: сервер держит буфер для такого соединения (AllowStatefulReconnects).
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => realtimeRetryDelayMs(context.previousRetryCount) })
    .withStatefulReconnect()
    .withServerTimeout(60_000)
    .configureLogging(LogLevel.Warning)
    .build();
}
