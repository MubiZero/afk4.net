import { afterEach, describe, expect, it, mock } from 'bun:test';
import { postHostWindowCommand, postHostWindowResize, postHostWindowTheme } from './hostBridge';

// Протокол моста проверяется в @afk4/host-bridge — один раз на обе оболочки. Здесь остаётся то,
// что принадлежит именно этому нативному хосту: имена оконных команд. Они разные у двух хостов,
// и именно на этом расхождении держался форк.

describe('postHostWindowCommand', () => {
  afterEach(() => {
    delete window.chrome;
  });

  it('posts a narrow window command to the native host', () => {
    const postMessage = mock();
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

describe('postHostWindowTheme', () => {
  afterEach(() => {
    delete window.chrome;
  });

  it('posts the current theme to the native host so it can swap the taskbar icon', () => {
    const postMessage = mock();
    window.chrome = {
      webview: {
        postMessage
      }
    };

    postHostWindowTheme('light');

    expect(postMessage).toHaveBeenCalledWith({ type: 'window:theme', theme: 'light' });
  });

  it('does nothing outside WebView2', () => {
    expect(() => postHostWindowTheme('dark')).not.toThrow();
  });
});

describe('postHostWindowResize', () => {
  afterEach(() => {
    delete window.chrome;
  });

  // Изменение размера за край понимает только хост оболочки: у мастера такой команды нет.
  it('posts the dragged edge to the native host', () => {
    const postMessage = mock();
    window.chrome = { webview: { postMessage } };

    postHostWindowResize('bottom-right');

    expect(postMessage).toHaveBeenCalledWith({ type: 'window:resize', edge: 'bottom-right' });
  });
});
