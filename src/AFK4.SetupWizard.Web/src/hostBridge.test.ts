import { afterEach, describe, expect, it, mock } from 'bun:test';
import { postHostWindowCommand, postHostWindowTheme } from './hostBridge';

// Копия моста у мастера не была покрыта тестами вовсе — она и разошлась с админской молча.
// Протокол теперь проверяется один раз в @afk4/host-bridge; здесь остаётся то, что принадлежит
// именно этому нативному хосту: имена оконных команд.
//
// `toggleMaximize`, а не `maximize`: так называет её WebViewSetupWindow.TryHandleWindowMessage.
// У оболочки оператора команда зовётся иначе, и общий список сломал бы одну из двух.
describe('postHostWindowCommand', () => {
  afterEach(() => {
    delete window.chrome;
  });

  it('шлёт команду окна под именем, которое понимает хост мастера', () => {
    const postMessage = mock();
    window.chrome = { webview: { postMessage } };

    postHostWindowCommand('toggleMaximize');

    expect(postMessage).toHaveBeenCalledWith({ type: 'window:toggleMaximize' });
  });

  it('вне WebView2 молчит, а не падает', () => {
    expect(() => postHostWindowCommand('close')).not.toThrow();
  });
});

describe('postHostWindowTheme', () => {
  afterEach(() => {
    delete window.chrome;
  });

  // Хост меняет по этому сообщению Window.Icon: без него знак в таскбаре оставался бы от чужой темы.
  it('шлёт текущую тему хосту', () => {
    const postMessage = mock();
    window.chrome = { webview: { postMessage } };

    postHostWindowTheme('dark');

    expect(postMessage).toHaveBeenCalledWith({ type: 'window:theme', theme: 'dark' });
  });
});
