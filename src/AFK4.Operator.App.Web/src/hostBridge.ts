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

export interface HostBridgeError {
  code: string;
  message: string;
}

export interface HostBridgeResponse<TPayload> {
  type: 'host:response';
  requestId: string;
  ok: boolean;
  payload?: TPayload;
  error?: HostBridgeError;
}

export const hostBridgeUnavailableMessage = 'Native host bridge is unavailable.';

export class HostBridgeUnavailableError extends Error {
  constructor() {
    super(hostBridgeUnavailableMessage);
    this.name = 'HostBridgeUnavailableError';
  }
}

export function isHostBridgeUnavailableError(error: unknown): boolean {
  return error instanceof HostBridgeUnavailableError
    || (error instanceof Error && error.message === hostBridgeUnavailableMessage);
}

export function postHostWindowCommand(command: HostWindowCommand): void {
  window.chrome?.webview?.postMessage({ type: `window:${command}` });
}

export function postHostWindowResize(edge: HostWindowResizeEdge): void {
  window.chrome?.webview?.postMessage({ type: 'window:resize', edge });
}

export function postHostRequest<TPayload>(
  type: string,
  payload?: unknown,
  timeoutMs = 15_000
): Promise<TPayload> {
  const webview = window.chrome?.webview;
  const postMessage = webview?.postMessage;
  const addEventListener = webview?.addEventListener;
  const removeEventListener = webview?.removeEventListener;
  if (!webview || !postMessage || !addEventListener || !removeEventListener) {
    return Promise.reject(new HostBridgeUnavailableError());
  }

  const sendMessage = postMessage.bind(webview);
  const addMessageListener = addEventListener.bind(webview);
  const removeMessageListener = removeEventListener.bind(webview);
  const requestId = createRequestId();
  return new Promise<TPayload>((resolve, reject) => {
    const timeout = window.setTimeout(() => {
      cleanup();
      reject(new Error('Native host bridge request timed out.'));
    }, timeoutMs);

    const onMessage = (event: HostBridgeMessageEvent) => {
      const response = event.data as Partial<HostBridgeResponse<TPayload>> | undefined;
      if (response?.type !== 'host:response' || response.requestId !== requestId) {
        return;
      }

      cleanup();
      if (response.ok) {
        resolve(response.payload as TPayload);
        return;
      }

      reject(new Error(response.error?.message ?? 'Native host bridge request failed.'));
    };

    function cleanup() {
      window.clearTimeout(timeout);
      removeMessageListener('message', onMessage);
    }

    addMessageListener('message', onMessage);
    sendMessage({ type, requestId, payload });
  });
}

function createRequestId(): string {
  return window.crypto?.randomUUID?.() ?? `request-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export interface HostBridgeMessageEvent {
  data: unknown;
}

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage(message: unknown): void;
        addEventListener?(type: 'message', listener: (event: HostBridgeMessageEvent) => void): void;
        removeEventListener?(type: 'message', listener: (event: HostBridgeMessageEvent) => void): void;
      };
    };
  }
}
