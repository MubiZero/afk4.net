declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage(message: unknown): void;
        addEventListener?(type: 'message', listener: (event: { data: unknown }) => void): void;
        removeEventListener?(type: 'message', listener: (event: { data: unknown }) => void): void;
      };
    };
  }
}

interface HostResponse<T> {
  type: 'host:response';
  requestId: string;
  ok: boolean;
  payload?: T;
  error?: { code: string; message: string };
}

let requestCounter = 0;

function nextRequestId(): string {
  requestCounter += 1;
  return `req-${requestCounter}`;
}

export function postShellRequest<T>(type: string, payload?: unknown, timeoutMs = 15_000): Promise<T> {
  const webview = window.chrome?.webview;
  if (!webview?.postMessage || !webview.addEventListener) {
    return Promise.reject(new Error('shell bridge unavailable'));
  }

  const requestId = nextRequestId();

  return new Promise<T>((resolve, reject) => {
    const listener = (event: { data: unknown }) => {
      const data = event.data as HostResponse<T>;
      if (!data || data.type !== 'host:response' || data.requestId !== requestId) {
        return;
      }
      cleanup();
      if (data.ok) {
        resolve(data.payload as T);
      } else {
        reject(new Error(data.error?.code ?? 'host_error'));
      }
    };

    const timer = setTimeout(() => {
      cleanup();
      reject(new Error('shell request timed out'));
    }, timeoutMs);

    function cleanup() {
      clearTimeout(timer);
      webview!.removeEventListener?.('message', listener);
    }

    webview.addEventListener?.('message', listener);
    webview.postMessage({ requestId, type, payload });
  });
}

export function onShellStateChanged(handler: (state: unknown) => void): () => void {
  const webview = window.chrome?.webview;
  if (!webview?.addEventListener) {
    return () => {};
  }

  const listener = (event: { data: unknown }) => {
    const data = event.data as { type?: string; payload?: unknown };
    if (data?.type === 'shell:stateChanged') {
      handler(data.payload);
    }
  };

  webview.addEventListener('message', listener);
  return () => webview.removeEventListener?.('message', listener);
}
