import { afterEach, describe, expect, it } from 'bun:test';
import { launchApp, loadShellState, pauseSession, requestOperator } from './shellClient';

function installWebview(onPost: (message: any) => void) {
  const listeners: Array<(event: { data: unknown }) => void> = [];
  (window as any).chrome = {
    webview: {
      postMessage: (message: any) => onPost(message),
      addEventListener: (_t: 'message', l: (event: { data: unknown }) => void) => listeners.push(l),
      removeEventListener: () => {}
    }
  };
  return { reply: (data: unknown) => listeners.forEach((l) => l({ data })) };
}

afterEach(() => {
  delete (window as any).chrome;
});

describe('shellClient', () => {
  it('launchApp sends launcher:launch with appId', async () => {
    let sent: any;
    const harness = installWebview((message) => {
      sent = message;
    });

    const promise = launchApp('cs2');
    harness.reply({ type: 'host:response', requestId: sent.requestId, ok: true, payload: { status: 'accepted' } });

    await promise;
    expect(sent.type).toBe('launcher:launch');
    expect(sent.payload).toEqual({ appId: 'cs2' });
  });

  it('loadShellState sends shell:loadState', async () => {
    let sent: any;
    const harness = installWebview((message) => {
      sent = message;
    });

    const promise = loadShellState();
    harness.reply({ type: 'host:response', requestId: sent.requestId, ok: true, payload: null });

    await expect(promise).resolves.toBeNull();
    expect(sent.type).toBe('shell:loadState');
  });

  it('requestOperator and pauseSession send their types', async () => {
    const posts: any[] = [];
    const harness = installWebview((message) => posts.push(message));

    const op = requestOperator();
    harness.reply({ type: 'host:response', requestId: posts[0].requestId, ok: true, payload: { requested: true } });
    await op;

    const pause = pauseSession();
    harness.reply({ type: 'host:response', requestId: posts[1].requestId, ok: true, payload: { paused: true } });
    await pause;

    expect(posts.map((p) => p.type)).toEqual(['shell:requestOperator', 'shell:pause']);
  });
});
