import { describe, expect, it, mock } from 'bun:test';
import { renderHook, waitFor, act } from '@testing-library/react';
import { usePlans } from './usePlans';

import type { ReactNode } from 'react';
import { I18nProvider } from '@/i18n/I18nProvider';

// Причина отказа теперь приходит из каталога строк (см. useLoadable), поэтому хук живёт внутри
// провайдера — как и в самом приложении.
function wrapper({ children }: { children: ReactNode }) {
  return <I18nProvider>{children}</I18nProvider>;
}

function fakeClient(over: Partial<Record<'listPlans', unknown>> = {}) {
  return { listPlans: mock().mockResolvedValue([]), ...over } as never;
}

describe('usePlans', () => {
  it('reaches ready', async () => {
    const { result } = renderHook(() => usePlans(fakeClient()), { wrapper });
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });

  it('errors then retry reloads', async () => {
    const client = fakeClient({ listPlans: mock().mockRejectedValueOnce(new Error('boom')).mockResolvedValue([]) });
    const { result } = renderHook(() => usePlans(client), { wrapper });
    await waitFor(() => expect(result.current.status).toBe('error'));
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
