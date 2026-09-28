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

/**
 * Сколько ждать сервер. Без предела подвисшая сеть клуба запирала экран: лист продления и раннего
 * выхода не закрыть, пока идёт отправка, а отправка не кончалась никогда. Превышение — отказ без
 * кода со статусом 0; ключ попытки экран держит до успеха, так что повтор деньги не задвоит.
 */
const REQUEST_TIMEOUT_MS = 20_000;

export function getJson<T>(baseUrl: string, path: string, signal?: AbortSignal): Promise<T> {
  return exchange<T>(baseUrl, path, { headers: { Accept: 'application/json' } }, signal);
}

export function postJson<T>(baseUrl: string, path: string, body: unknown, timeoutMs = REQUEST_TIMEOUT_MS): Promise<T> {
  return exchange<T>(baseUrl, path, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  }, undefined, timeoutMs);
}

async function exchange<T>(
  baseUrl: string,
  path: string,
  init: RequestInit,
  callerSignal?: AbortSignal,
  timeoutMs = REQUEST_TIMEOUT_MS
): Promise<T> {
  const timeout = AbortSignal.timeout(timeoutMs);
  const signal = callerSignal === undefined ? timeout : AbortSignal.any([callerSignal, timeout]);
  try {
    return await readResponse<T>(await fetch(new URL(path, baseUrl), { ...init, signal }));
  } catch (cause) {
    // Отмена вызывающим (ушёл с экрана) — не ошибка, пусть уходит как есть.
    if (timeout.aborted && callerSignal?.aborted !== true) throw new PlayerApiError(0, null);
    throw cause;
  }
}

/**
 * Событие окна: сервер ответил 401 — вход игрока на этом ПК погашен. Экран слушает его и выходит,
 * а не показывает «не получилось» под чужим именем.
 */
export const PLAYER_UNAUTHORIZED_EVENT = 'afk4:player-unauthorized';

async function readResponse<T>(response: Response): Promise<T> {
  if (response.ok) {
    return (response.status === 204 ? null : await response.json()) as T;
  }

  if (response.status === 401) window.dispatchEvent(new Event(PLAYER_UNAUTHORIZED_EVENT));

  let code: string | null = null;
  try {
    const body = (await response.json()) as { error?: unknown; code?: unknown };
    code = typeof body.error === 'string' ? body.error : typeof body.code === 'string' ? body.code : null;
  } catch {
    // Тело не JSON — отказ без кода, экран скажет общее «не получилось».
  }
  throw new PlayerApiError(response.status, code);
}
