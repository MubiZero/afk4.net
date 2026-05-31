import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { usePlans } from './usePlans';

function fakeClient(over: Partial<Record<'listPlans', unknown>> = {}) {
  return { listPlans: vi.fn().mockResolvedValue([]), ...over } as never;
}

describe('usePlans', () => {
  it('reaches ready', async () => {
    const { result } = renderHook(() => usePlans(fakeClient()));
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });

  it('errors then retry reloads', async () => {
    const client = fakeClient({ listPlans: vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValue([]) });
    const { result } = renderHook(() => usePlans(client));
    await waitFor(() => expect(result.current.status).toBe('error'));
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
