import type { HostBridgeMessageEvent } from '@afk4/host-bridge';
import { ShellBridgeRequestTypeNames, type PlayerShellStateDto, type ShellAuthStateDto } from '@afk4/contracts';

/**
 * Хост для тестов: отвечает на снимок, записывает запросы и умеет прислать событие сам.
 * Ответы на остальные запросы задаёт тест.
 */
export function installFakeHost(options: {
  state: PlayerShellStateDto | null;
  auth?: ShellAuthStateDto;
  reply?: (type: string, payload: unknown) => { ok: boolean; payload?: unknown; error?: { code: string; message: string } };
}) {
  const listeners = new Set<(event: HostBridgeMessageEvent) => void>();
  const requests: { type: string; payload: unknown }[] = [];
  const emit = (data: unknown) => queueMicrotask(() => {
    for (const listener of listeners) listener({ data });
  });

  window.chrome = {
    webview: {
      postMessage(message: unknown) {
        const request = message as { type: string; requestId: string; payload: unknown };
        requests.push({ type: request.type, payload: request.payload });
        if (request.type === ShellBridgeRequestTypeNames.ShellReady) {
          emit({
            type: 'host:response',
            requestId: request.requestId,
            ok: true,
            payload: { state: options.state, auth: options.auth ?? { signedIn: false } }
          });
          return;
        }
        const result = options.reply?.(request.type, request.payload) ?? { ok: true, payload: {} };
        emit({ type: 'host:response', requestId: request.requestId, ...result });
      },
      addEventListener: (_type, listener) => listeners.add(listener),
      removeEventListener: (_type, listener) => listeners.delete(listener)
    }
  };

  return {
    requests,
    send: (type: string, payload?: unknown) => emit({ type, payload })
  };
}
