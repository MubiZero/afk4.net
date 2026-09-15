import { postHostRequest as postRequest, postHostWindowMessage } from '@afk4/host-bridge';

/**
 * Мост мастера к его нативному хосту.
 *
 * Протокол — общий (`@afk4/host-bridge`). Здесь остаётся только то, что принадлежит ИМЕННО
 * этому хосту: набор оконных команд и таймаут. Раньше весь файл был форкнут от админского и
 * разошёлся с ним по обоим этим пунктам плюс по поведению таймаута.
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

/** Команды, которые понимает WebViewSetupWindow.TryHandleWindowMessage. */
export type HostWindowCommand = 'drag' | 'minimize' | 'toggleMaximize' | 'close';

export function postHostWindowCommand(command: HostWindowCommand): void {
  postHostWindowMessage({ type: `window:${command}` });
}

// Синхронизирует иконку окна/таскбара с темой мастера — нативный хост меняет Window.Icon
// (см. WebViewSetupWindow.TryHandleWindowMessage "window:theme").
export function postHostWindowTheme(theme: 'light' | 'dark'): void {
  postHostWindowMessage({ type: 'window:theme', theme });
}

/**
 * Тридцать секунд, а не пятнадцать как у админки: шаги мастера ходят к платформе через хост —
 * вход, SMS, подтверждение устройства, — и на медленной линии первого клуба пятнадцати не хватает.
 */
const HOST_REQUEST_TIMEOUT_MS = 30_000;

export function postHostRequest<TPayload>(
  type: string,
  payload?: unknown,
  timeoutMs = HOST_REQUEST_TIMEOUT_MS
): Promise<TPayload> {
  return postRequest<TPayload>(type, payload, timeoutMs);
}
