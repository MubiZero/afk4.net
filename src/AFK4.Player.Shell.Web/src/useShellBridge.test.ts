import { afterEach, describe, expect, it } from 'bun:test';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useShellBridge } from './useShellBridge';

function installWebview(onPost: (message: any) => void) {
  const listeners: Array<(event: { data: unknown }) => void> = [];
  (window as any).chrome = {
    webview: {
      postMessage: (message: any) => onPost(message),
      addEventListener: (_t: 'message', l: (event: { data: unknown }) => void) => listeners.push(l),
      removeEventListener: (_t: 'message', l: (event: { data: unknown }) => void) => {
        const i = listeners.indexOf(l);
        if (i >= 0) listeners.splice(i, 1);
      }
    }
  };
  return { push: (data: unknown) => act(() => listeners.forEach((l) => l({ data }))) };
}

afterEach(() => {
  delete (window as any).chrome;
});

describe('useShellBridge', () => {
  it('starts with null state then updates on shell:stateChanged pushes', async () => {
    const harness = installWebview(() => {});
    const { result } = renderHook(() => useShellBridge());

    expect(result.current.state).toBeNull();

    harness.push({ type: 'shell:stateChanged', payload: { state: 'active', remainingSeconds: 600, launcherApps: [] } });

    await waitFor(() => expect(result.current.state?.state).toBe('active'));
  });
});
