import { afterEach, describe, expect, it, mock } from 'bun:test';
import {
  HostBridgeRequestError,
  HostBridgeUnavailableError,
  hostBridgeTimeoutCode,
  isHostBridgeAvailable,
  isHostBridgeUnavailableError,
  postHostRequest,
  postHostWindowMessage,
  type HostBridgeMessageEvent
} from './index';

/** Хост, который отвечает на каждый запрос заданным телом. */
function respondingHost(reply: (requestId: string) => unknown) {
  const listeners = new Set<(event: HostBridgeMessageEvent) => void>();
  const postMessage = mock((message: unknown) => {
    const request = message as { requestId: string };
    queueMicrotask(() => {
      for (const listener of listeners) listener({ data: reply(request.requestId) });
    });
  });
  window.chrome = {
    webview: {
      postMessage,
      addEventListener: (_type, listener) => listeners.add(listener),
      removeEventListener: (_type, listener) => listeners.delete(listener)
    }
  };
  return { postMessage, listeners };
}

/** Хост, который принимает запрос и молчит. */
function silentHost() {
  const listeners = new Set<(event: HostBridgeMessageEvent) => void>();
  window.chrome = {
    webview: {
      postMessage: () => {},
      addEventListener: (_type, listener) => listeners.add(listener),
      removeEventListener: (_type, listener) => listeners.delete(listener)
    }
  };
  return listeners;
}

afterEach(() => {
  delete window.chrome;
  mock.restore();
});

describe('postHostRequest', () => {
  it('отправляет запрос и разрешает свой ответ хоста', async () => {
    const { postMessage } = respondingHost((requestId) => ({
      type: 'host:response', requestId, ok: true, payload: { displayName: 'Cashier One' }
    }));

    await expect(postHostRequest<{ displayName: string }>('auth:loadToken', undefined, 5_000))
      .resolves.toEqual({ displayName: 'Cashier One' });
    expect(postMessage).toHaveBeenCalledWith(expect.objectContaining({
      type: 'auth:loadToken', requestId: expect.any(String)
    }));
  });

  // Ответ на ЧУЖОЙ запрос не должен разрешать этот: в окне живёт один слушатель на все запросы.
  it('не путает ответ на чужой запрос со своим', async () => {
    const listeners = silentHost();
    const pending = postHostRequest('auth:signIn', {}, 5_000);

    for (const listener of listeners) {
      listener({ data: { type: 'host:response', requestId: 'someone-else', ok: true, payload: 'чужое' } });
    }

    const settled = await Promise.race([pending.then(() => 'решился'), Promise.resolve('ещё ждёт')]);
    expect(settled).toBe('ещё ждёт');
  });

  it('отвергает отказ хоста с его кодом и остатком попыток', async () => {
    respondingHost((requestId) => ({
      type: 'host:response',
      requestId,
      ok: false,
      error: { code: 'invalid_code', message: 'bad code', remainingAttempts: 2 }
    }));

    await expect(postHostRequest('auth:resetByPhone', {}, 5_000)).rejects.toMatchObject({
      code: 'invalid_code', remainingAttempts: 2, message: 'bad code'
    });
  });

  // Главное расхождение двух форков: у админки таймаут отвергался безымянным Error, и экран не
  // отличал «мост молчит» от «сервер отказал» — показывал английскую строку вместо объяснения.
  it('отвергает таймаут своим кодом, а не безымянной ошибкой', async () => {
    silentHost();

    const error = await postHostRequest('auth:loadToken', undefined, 5).catch((reason) => reason);

    expect(error).toBeInstanceOf(HostBridgeRequestError);
    expect((error as HostBridgeRequestError).code).toBe(hostBridgeTimeoutCode);
    expect((error as HostBridgeRequestError).remainingAttempts).toBeNull();
  });

  // Слушатель снимается и по таймауту: иначе каждый молчаливый запрос оставлял бы свой навсегда.
  it('снимает слушателя после таймаута', async () => {
    const listeners = silentHost();

    await postHostRequest('auth:loadToken', undefined, 5).catch(() => {});

    expect(listeners.size).toBe(0);
  });

  it('отвергает запрос, когда моста нет вовсе', async () => {
    await expect(postHostRequest('auth:loadToken', undefined, 5_000))
      .rejects.toThrow(HostBridgeUnavailableError);
  });
});

describe('isHostBridgeAvailable', () => {
  it('в браузере без хоста отвечает «нет»', () => {
    expect(isHostBridgeAvailable()).toBe(false);
  });

  // Половинчатый мост — тоже «нет»: postHostRequest на нём всё равно откажет, и кнопка,
  // показанная по такому ответу, обещала бы невыполнимое.
  it('считает недоступным мост без слушателей', () => {
    window.chrome = { webview: { postMessage: () => {} } };
    expect(isHostBridgeAvailable()).toBe(false);
  });

  it('полный мост считает доступным', () => {
    window.chrome = {
      webview: { postMessage: () => {}, addEventListener: () => {}, removeEventListener: () => {} }
    };
    expect(isHostBridgeAvailable()).toBe(true);
  });
});

describe('postHostWindowMessage', () => {
  it('передаёт сообщение окну как есть', () => {
    const postMessage = mock();
    window.chrome = { webview: { postMessage } };

    postHostWindowMessage({ type: 'window:theme', theme: 'light' });

    expect(postMessage).toHaveBeenCalledWith({ type: 'window:theme', theme: 'light' });
  });

  it('вне WebView2 молчит, а не падает', () => {
    expect(() => postHostWindowMessage({ type: 'window:close' })).not.toThrow();
  });
});

describe('isHostBridgeUnavailableError', () => {
  it('отличает отсутствие моста от его молчания', () => {
    expect(isHostBridgeUnavailableError(new HostBridgeUnavailableError())).toBe(true);
    expect(isHostBridgeUnavailableError(new Error('Native host bridge is unavailable.'))).toBe(true);
    expect(isHostBridgeUnavailableError(
      new HostBridgeRequestError('timed out', hostBridgeTimeoutCode, null))).toBe(false);
  });
});
