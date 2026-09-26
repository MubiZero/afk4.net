import type { PlayerShellStateDto } from '@afk4/contracts';

/**
 * Запросы экрана к серверу клуба (спека, §4.4): `fetch` на адрес API, который назвал агент.
 * Заголовок с токеном подставляет хост — страница токенов не видит и сама их не шлёт.
 */
export class PlayerApiError extends Error {
  constructor(
    public readonly status: number,
    /** Код отказа сервера (`insufficient_balance`, `seating_code_invalid`); null — сбой без кода. */
    public readonly code: string | null
  ) {
    super(code ?? `HTTP ${status}`);
    this.name = 'PlayerApiError';
  }
}

declare global {
  interface Window {
    __AFK4_PLAYER_CONFIG__?: { platformBaseUrl?: string };
  }
}

/** Адрес API: агента — в первую очередь, это тот же адрес, для которого хост подставляет токен. */
export function apiBaseUrl(state: PlayerShellStateDto | null): string | null {
  return state?.apiBaseUrl || window.__AFK4_PLAYER_CONFIG__?.platformBaseUrl || null;
}

export async function getJson<T>(baseUrl: string, path: string, signal?: AbortSignal): Promise<T> {
  return readResponse<T>(await fetch(new URL(path, baseUrl), { signal, headers: { Accept: 'application/json' } }));
}

export async function postJson<T>(baseUrl: string, path: string, body: unknown): Promise<T> {
  return readResponse<T>(
    await fetch(new URL(path, baseUrl), {
      method: 'POST',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    })
  );
}

async function readResponse<T>(response: Response): Promise<T> {
  if (response.ok) {
    return (response.status === 204 ? null : await response.json()) as T;
  }

  let code: string | null = null;
  try {
    const body = (await response.json()) as { error?: unknown; code?: unknown };
    code = typeof body.error === 'string' ? body.error : typeof body.code === 'string' ? body.code : null;
  } catch {
    // Тело не JSON — отказ без кода, экран скажет общее «не получилось».
  }
  throw new PlayerApiError(response.status, code);
}
