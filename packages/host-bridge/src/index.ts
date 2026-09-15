/**
 * Мост между веб-частью и нативным хостом WebView2 — один на обе оболочки.
 *
 * Раньше их было два: файл мастера был форкнут от админского и разошёлся. У мастера таймаут
 * отвергался своим кодом (`host_timeout`), у админки — безымянным `Error`, из-за чего экран не
 * отличал «мост молчит» от «сервер отказал» и показывал английскую строку вместо объяснения.
 * У админки была проверка `isHostBridgeAvailable`, которой не было у мастера. Ни одна из копий
 * не была покрыта тестами целиком.
 *
 * Здесь живёт только протокол: запрос-ответ, ошибки, таймаут, доступность. Оконные команды
 * остаются в приложениях — их наборы у двух нативных хостов РАЗНЫЕ (мастер понимает
 * `toggleMaximize` и не понимает `resize`, у админки наоборот), и сводить их к одному списку
 * значило бы объявить команду, на которую один из хостов не ответит.
 */

export interface HostBridgeError {
  code: string;
  message: string;
  remainingAttempts?: number | null;
}

export interface HostBridgeResponse<TPayload> {
  type: 'host:response';
  requestId: string;
  ok: boolean;
  payload?: TPayload;
  error?: HostBridgeError;
}

export interface HostBridgeMessageEvent {
  data: unknown;
}

export const hostBridgeUnavailableMessage = 'Native host bridge is unavailable.';

/** Код отказа «мост не ответил вовремя». Отличается от кодов, которые присылает сам мост. */
export const hostBridgeTimeoutCode = 'host_timeout';

export class HostBridgeUnavailableError extends Error {
  constructor() {
    super(hostBridgeUnavailableMessage);
    this.name = 'HostBridgeUnavailableError';
  }
}

/**
 * Операция моста отказала со структурированной ошибкой (например `invalid_code` с остатком
 * попыток от маршрута сброса по SMS). Несёт код и остаток, чтобы экран показал конкретную
 * причину, а не общее «что-то пошло не так».
 */
export class HostBridgeRequestError extends Error {
  constructor(
    message: string,
    public readonly code: string,
    public readonly remainingAttempts: number | null,
  ) {
    super(message);
    this.name = 'HostBridgeRequestError';
  }
}

export function isHostBridgeUnavailableError(error: unknown): boolean {
  return error instanceof HostBridgeUnavailableError
    || (error instanceof Error && error.message === hostBridgeUnavailableMessage);
}

/**
 * Тот же набор проверок, что делает `postHostRequest` перед отправкой. Нужен экранам, которые
 * предлагают действие самого приложения (перезапуск ради обновления): в браузере хоста нет, и
 * показывать кнопку, которая гарантированно откажет, — обещание, которое нечем выполнить.
 */
export function isHostBridgeAvailable(): boolean {
  const webview = window.chrome?.webview;
  return Boolean(webview?.postMessage && webview?.addEventListener && webview?.removeEventListener);
}

/**
 * Односторонняя команда окну. Набор допустимых `type` знает приложение: он задан его нативным
 * хостом, а не этим пакетом.
 */
export function postHostWindowMessage(message: { type: string } & Record<string, unknown>): void {
  window.chrome?.webview?.postMessage(message);
}

/** Запрос к хосту с ожиданием ответа. Таймаут задаёт приложение — у оболочек он разный. */
export function postHostRequest<TPayload>(
  type: string,
  payload: unknown,
  timeoutMs: number
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
      // Своим кодом, а не безымянным Error: иначе экран не отличит «мост молчит» от «сервер
      // отказал» и покажет английскую строку вместо объяснения.
      reject(new HostBridgeRequestError(
        'Native host bridge request timed out.', hostBridgeTimeoutCode, null));
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

      reject(new HostBridgeRequestError(
        response.error?.message ?? 'Native host bridge request failed.',
        response.error?.code ?? 'host_error',
        response.error?.remainingAttempts ?? null,
      ));
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
