import { it, expect, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import type { AuditSearchResult } from '@/api/types';
import { useAudit } from './useAudit';

const result: AuditSearchResult = { limit: 100, records: [] };

it('loads audit records into the ready state', async () => {
  const client = { searchAudit: mock<() => Promise<AuditSearchResult>>(async () => result) };
  const { result: hook } = renderHook(() => useAudit(client as never, 'b1', { limit: 100 }));
  await waitFor(() => expect(hook.current.status).toBe('ready'));
  expect(client.searchAudit).toHaveBeenCalledWith('b1', { limit: 100 });
});

it('reports an error when the load fails', async () => {
  const client = { searchAudit: mock<() => Promise<AuditSearchResult>>(async () => { throw new Error('boom'); }) };
  const { result: hook } = renderHook(() => useAudit(client as never, 'b1', { limit: 100 }));
  await waitFor(() => expect(hook.current.status).toBe('error'));
});
