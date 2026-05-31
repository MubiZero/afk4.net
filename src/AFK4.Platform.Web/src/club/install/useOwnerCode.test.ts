import { it, expect, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import type { OwnerCodeSummary } from '@/api/types';
import { useOwnerCode } from './useOwnerCode';

const summary: OwnerCodeSummary = { codeSuffix: '5678', expiresAtUtc: '2026-06-01T00:00:00.000Z', lastUsedAtUtc: null, failedAttemptCount: 0 };

it('loads the owner-code summary when enabled', async () => {
  const client = { getOwnerCode: mock<() => Promise<OwnerCodeSummary | null>>(async () => summary) };
  const { result } = renderHook(() => useOwnerCode(client as never, true));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.summary).toEqual(summary);
});

it('does not fetch when disabled', async () => {
  const client = { getOwnerCode: mock<() => Promise<OwnerCodeSummary | null>>(async () => summary) };
  const { result } = renderHook(() => useOwnerCode(client as never, false));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  expect(client.getOwnerCode).not.toHaveBeenCalled();
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.summary).toBeNull();
});

it('reports an error when the load fails', async () => {
  const client = { getOwnerCode: mock<() => Promise<OwnerCodeSummary | null>>(async () => { throw new Error('boom'); }) };
  const { result } = renderHook(() => useOwnerCode(client as never, true));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
