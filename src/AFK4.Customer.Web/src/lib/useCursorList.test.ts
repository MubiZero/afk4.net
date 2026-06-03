import { it, expect, mock } from 'bun:test';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useCursorList, type CursorListState } from './useCursorList';

type ReadyState<T> = Extract<CursorListState<T>, { status: 'ready' }>;
function asReady<T>(s: CursorListState<T>): ReadyState<T> {
  if (s.status !== 'ready') throw new Error('not ready');
  return s;
}

it('loads the first page and exposes hasMore from nextCursor', async () => {
  const fetchPage = mock().mockResolvedValue({ items: [{ id: 'a' }], nextCursor: 'C2' });
  const { result } = renderHook(() => useCursorList(fetchPage));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  const state1 = asReady(result.current);
  expect(state1.items).toEqual([{ id: 'a' }]);
  expect(state1.hasMore).toBe(true);
});

it('appends the next page on loadMore and clears hasMore when exhausted', async () => {
  const fetchPage = mock()
    .mockResolvedValueOnce({ items: [{ id: 'a' }], nextCursor: 'C2' })
    .mockResolvedValueOnce({ items: [{ id: 'b' }], nextCursor: null });
  const { result } = renderHook(() => useCursorList(fetchPage));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  await act(async () => { asReady(result.current).loadMore(); });
  await waitFor(() => {
    const s = asReady(result.current);
    expect(s.items).toEqual([{ id: 'a' }, { id: 'b' }]);
    expect(s.hasMore).toBe(false);
  });
  expect(fetchPage.mock.calls[1][0]).toBe('C2');
});

it('reports an error state on a failed first page', async () => {
  const fetchPage = mock().mockRejectedValue(new Error('boom'));
  const { result } = renderHook(() => useCursorList(fetchPage));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
