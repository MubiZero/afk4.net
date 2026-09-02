import { afterEach, describe, expect, it } from 'bun:test';
import { onShellStateChanged, postShellRequest } from './shellBridge';

type Listener = (event: { data: unknown }) => void;

function installWebview(onPost: (message: any) => void) {
  const listeners: Listener[] = [];
  (window as any).chrome = {
    webview: {
      postMessage: (message: any) => onPost(message),
      addEventListener: (_type: 'message', listener: Listener) => listeners.push(listener),
      removeEventListener: (_type: 'message', listener: Listener) => {
        const i = listeners.indexOf(listener);
        if (i >= 0) listeners.splice(i, 1);
      }
    }
  };
  return {
    emit: (data: unknown) => {
      listeners.forEach((l) => {
        l({ data });
      });
    }
  };
}

afterEach(() => {
  delete (window as any).chrome;
});

describe('postShellRequest', () => {
  it('resolves with the payload of the matching host:response', async () => {
    let sent: any;
    const harness = installWebview((message) => {
      sent = message;
    });

    const promise = postShellRequest<{ paused: boolean }>('shell:pause');
    harness.emit({ type: 'host:response', requestId: sent.requestId, ok: true, payload: { paused: true } });

    await expect(promise).resolves.toEqual({ paused: true });
    expect(sent.type).toBe('shell:pause');
  });

  it('rejects when the host responds with ok=false', async () => {
    let sent: any;
    const harness = installWebview((message) => {
      sent = message;
    });

    const promise = postShellRequest('launcher:launch', { appId: '' });
    harness.emit({
      type: 'host:response',
      requestId: sent.requestId,
      ok: false,
      error: { code: 'invalid_payload', message: 'bad' }
    });

    await expect(promise).rejects.toThrow('invalid_payload');
  });
});

describe('onShellStateChanged', () => {
  it('invokes the listener on shell:stateChanged pushes', () => {
    const harness = installWebview(() => {});
    const seen: unknown[] = [];

    const unsubscribe = onShellStateChanged((state) => seen.push(state));
    harness.emit({ type: 'shell:stateChanged', payload: { state: 'Active' } });
    unsubscribe();
    harness.emit({ type: 'shell:stateChanged', payload: { state: 'Locked' } });

    expect(seen).toEqual([{ state: 'Active' }]);
  });
});
