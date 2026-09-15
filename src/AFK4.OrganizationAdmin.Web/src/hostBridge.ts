import { postHostRequest as postRequest, postHostWindowMessage } from '@afk4/host-bridge';

/**
 * Мост оболочки оператора к её нативному хосту.
 *
 * Протокол — общий (`@afk4/host-bridge`). Здесь остаётся только то, что принадлежит ИМЕННО
 * этому хосту: набор оконных команд, изменение размера за край и таймаут.
 */
export {
  hostBridgeTimeoutCode,
  hostBridgeUnavailableMessage,
  HostBridgeRequestError,
  HostBridgeUnavailableError,
  isHostBridgeAvailable,
  isHostBridgeUnavailableError,
  type HostBridgeError,
  type HostBridgeMessageEvent,
  type HostBridgeResponse
} from '@afk4/host-bridge';

/** Команды, которые понимает OrganizationAdminWindow.TryHandleWindowMessage. */
export type HostWindowCommand = 'drag' | 'minimize' | 'maximize' | 'close';

export type HostWindowResizeEdge =
  | 'left'
  | 'right'
  | 'top'
  | 'top-left'
  | 'top-right'
  | 'bottom'
  | 'bottom-left'
  | 'bottom-right';

export function postHostWindowCommand(command: HostWindowCommand): void {
  postHostWindowMessage({ type: `window:${command}` });
}

export function postHostWindowResize(edge: HostWindowResizeEdge): void {
  postHostWindowMessage({ type: 'window:resize', edge });
}

// Синхронизирует иконку окна/таскбара с темой оператора — нативный хост меняет Window.Icon
// (см. OrganizationAdminWindow.TryHandleWindowMessage "window:theme").
export function postHostWindowTheme(theme: 'light' | 'dark'): void {
  postHostWindowMessage({ type: 'window:theme', theme });
}

/** Пятнадцать секунд: запросы оболочки к хосту короткие и локальные. */
const HOST_REQUEST_TIMEOUT_MS = 15_000;

export function postHostRequest<TPayload>(
  type: string,
  payload?: unknown,
  timeoutMs = HOST_REQUEST_TIMEOUT_MS
): Promise<TPayload> {
  return postRequest<TPayload>(type, payload, timeoutMs);
}
