import { it, expect, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import { useReport } from './useReport';

it('loads data into the ready state', async () => {
  const loader = mock<() => Promise<{ n: number }>>(async () => ({ n: 7 }));
  const { result } = renderHook(() => useReport(loader, ['k']));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.data.n).toBe(7);
});

it('reports an error when the load fails', async () => {
  const loader = mock<() => Promise<{ n: number }>>(async () => { throw new Error('boom'); });
  const { result } = renderHook(() => useReport(loader, ['k']));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
