export type HostWindowCommand = 'drag' | 'minimize' | 'maximize' | 'close';

export function postHostWindowCommand(command: HostWindowCommand): void {
  window.chrome?.webview?.postMessage({ type: `window:${command}` });
}

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage(message: unknown): void;
      };
    };
  }
}
