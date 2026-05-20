import { afterEach, describe, expect, it, vi } from 'vitest';
import { postHostWindowCommand } from './hostBridge';

describe('postHostWindowCommand', () => {
  afterEach(() => {
    delete window.chrome;
  });

  it('posts a narrow window command to the native host', () => {
    const postMessage = vi.fn();
    window.chrome = {
      webview: {
        postMessage
      }
    };

    postHostWindowCommand('drag');

    expect(postMessage).toHaveBeenCalledWith({ type: 'window:drag' });
  });

  it('does nothing outside WebView2', () => {
    expect(() => postHostWindowCommand('close')).not.toThrow();
  });
});
