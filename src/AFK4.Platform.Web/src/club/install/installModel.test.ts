import { it, expect } from 'vitest';
import { toOwnerCodeView, getSetupMsiUrl } from './installModel';

it('shows the full issued code when freshly issued', () => {
  const view = toOwnerCodeView(null, { ownerCode: '12345678', codeSuffix: '5678', expiresAtUtc: '2026-06-01T00:00:00.000Z' });
  expect(view).toEqual({ code: '12345678', hasCode: true, expiresAtUtc: '2026-06-01T00:00:00.000Z', lastUsedAtUtc: null, failedAttemptCount: 0 });
});

it('masks the code from a summary', () => {
  const view = toOwnerCodeView({ codeSuffix: '5678', expiresAtUtc: '2026-06-01T00:00:00.000Z', lastUsedAtUtc: '2026-05-30T00:00:00.000Z', failedAttemptCount: 2 }, null);
  expect(view.code).toBe('**** 5678');
  expect(view.hasCode).toBe(true);
  expect(view.failedAttemptCount).toBe(2);
});

it('reports no code when both are null', () => {
  const view = toOwnerCodeView(null, null);
  expect(view.hasCode).toBe(false);
  expect(view.code).toBe('—');
});

it('falls back to the default MSI url when env is unset', () => {
  expect(getSetupMsiUrl()).toBe('/downloads/AFK4-Agent.msi');
});
