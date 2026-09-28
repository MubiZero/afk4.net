import { afterEach, expect, it } from 'bun:test';
import { PlayerApiError, getJson, postJson } from './playerApi';

const realFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = realFetch;
});

function silentServer() {
  globalThis.fetch = ((_input: RequestInfo | URL, init?: RequestInit) =>
    new Promise<Response>((_resolve, reject) => {
      init?.signal?.addEventListener('abort', () => reject(init.signal?.reason));
    })) as typeof fetch;
}

// Подвисшая сеть клуба запирала лист продления и раннего выхода: закрыть его нельзя, пока идёт
// отправка, а отправка не кончалась. Теперь запрос кончается отказом без кода — экран разблокируется.
it('gives up on a silent server instead of holding the screen forever', async () => {
  silentServer();

  const failure = await postJson('http://api.test/', '/api/me/sessions/s1/extend', {}, 20).catch((error: unknown) => error);

  expect(failure).toBeInstanceOf(PlayerApiError);
  expect((failure as PlayerApiError).status).toBe(0);
  expect((failure as PlayerApiError).code).toBeNull();
});

// Ушёл с экрана — это не сбой, и показывать по нему нечего.
it('passes the caller cancellation through untouched', async () => {
  silentServer();
  const controller = new AbortController();

  const pending = getJson('http://api.test/', '/api/me/this-pc/start-offers', controller.signal).catch((error: unknown) => error);
  controller.abort();
  const failure = await pending;

  expect(failure).not.toBeInstanceOf(PlayerApiError);
  expect((failure as Error).name).toBe('AbortError');
});
