import { describe, expect, it, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { PlatformApiError, TransportErrorCodes } from '@/api/platformApi';
import { useLoadable } from './useLoadable';

function wrapper({ children }: { children: ReactNode }) {
  return <I18nProvider>{children}</I18nProvider>;
}

describe('useLoadable', () => {
  it('отдаёт данные, когда запрос прошёл', async () => {
    const { result } = renderHook(() => useLoadable(() => Promise.resolve([1, 2]), []), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(result.current.status === 'ready' && result.current.data).toEqual([1, 2]);
  });

  // Сотрудник без права на раздел должен читать «недостаточно прав», а не «не удалось загрузить
  // данные»: первое он несёт начальнику, второе — заставляет жать «Повторить» до бесконечности.
  it('называет причину отказа словами: нет прав', async () => {
    const { result } = renderHook(
      () => useLoadable(() => Promise.reject(new PlatformApiError(403, 'Forbidden')), []),
      { wrapper });

    await waitFor(() => expect(result.current.status).toBe('error'));
    expect(result.current.status === 'error' && result.current.message).toBe('Недостаточно прав для этого действия.');
  });

  it('называет причину отказа словами: сервер не ответил', async () => {
    const { result } = renderHook(
      () => useLoadable(() => Promise.reject(new PlatformApiError(0, 'timed out', TransportErrorCodes.Timeout)), []),
      { wrapper });

    await waitFor(() => expect(result.current.status).toBe('error'));
    expect(result.current.status === 'error' && result.current.message).toBe('Сервер платформы не ответил вовремя. Повторите попытку.');
  });

  it('повтор загружает заново', async () => {
    const load = mock().mockRejectedValueOnce(new PlatformApiError(500, 'boom')).mockResolvedValue(['ок']);
    const { result } = renderHook(() => useLoadable(load, []), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('error'));
    result.current.retry();

    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(load).toHaveBeenCalledTimes(2);
  });

  it('смена того, о чём спрашивали, загружает заново', async () => {
    const load = mock().mockResolvedValue(['ок']);
    const { rerender, result } = renderHook(({ id }: { id: string }) => useLoadable(() => load(id), [id]), {
      wrapper,
      initialProps: { id: 'org-1' }
    });

    await waitFor(() => expect(result.current.status).toBe('ready'));
    rerender({ id: 'org-2' });

    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
    expect(load.mock.calls.map(call => call[0])).toEqual(['org-1', 'org-2']);
  });
});

// Дежурный экран держат открытым весь день. Фоновое обновление не должно подменять содержимое
// ожиданием: мигающий раз в минуту скелетон сделал бы такой экран нечитаемым.
it('фоновое обновление не стирает то, что уже на экране', async () => {
  const load = mock()
    .mockResolvedValueOnce(['было'])
    .mockResolvedValue(['стало']);
  const seen: string[] = [];
  const { result } = renderHook(() => {
    const state = useLoadable(load, [], { refreshMs: 50 });
    seen.push(state.status);
    return state;
  }, { wrapper });

  await waitFor(() => expect(result.current.status === 'ready' && result.current.data).toEqual(['стало']));

  // Ожидание бывает только до первого ответа: как только на экране что-то есть, фоновое
  // обновление его не подменяет.
  const afterFirstAnswer = seen.slice(seen.indexOf('ready'));
  expect(afterFirstAnswer).not.toContain('loading');
});

// Отказ по правам не чинится повтором: раздел обязан сказать это экрану, иначе тот рисует
// кнопку, которая заведомо не поможет, и человек жмёт её вместо того, чтобы просить доступ.
it('отличает отказ по правам от того, что стоит повторить', async () => {
  const forbidden = renderHook(() => useLoadable(() => Promise.reject(new PlatformApiError(403, 'Forbidden')), []), { wrapper });
  await waitFor(() => expect(forbidden.result.current.status).toBe('error'));
  expect(forbidden.result.current.status === 'error' && forbidden.result.current.canRetry).toBe(false);

  const serverDown = renderHook(() => useLoadable(() => Promise.reject(new PlatformApiError(500, 'Server error')), []), { wrapper });
  await waitFor(() => expect(serverDown.result.current.status).toBe('error'));
  expect(serverDown.result.current.status === 'error' && serverDown.result.current.canRetry).toBe(true);
});
