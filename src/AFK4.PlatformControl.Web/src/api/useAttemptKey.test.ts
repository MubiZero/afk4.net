import { describe, expect, it } from 'bun:test';
import { renderHook } from '@testing-library/react';
import { useAttemptKey } from './useAttemptKey';

describe('useAttemptKey', () => {
  it('повтор той же попытки несёт тот же ключ', () => {
    const { result } = renderHook(() => useAttemptKey());
    const body = { amountMinorUnits: 5000 };

    expect(result.current.forSubject(body)).toBe(result.current.forSubject(body));
  });

  // Иначе форма после правки суммы перестала бы отправляться: сервер отвергает тот же ключ с
  // другим телом (422, «ключ повторён с другим запросом»).
  it('исправленная форма — уже другая попытка', () => {
    const { result } = renderHook(() => useAttemptKey());

    const first = result.current.forSubject({ amountMinorUnits: 5000 });
    const second = result.current.forSubject({ amountMinorUnits: 7000 });

    expect(second).not.toBe(first);
  });

  it('после успеха начинается новая попытка', () => {
    const { result } = renderHook(() => useAttemptKey());
    const body = { amountMinorUnits: 5000 };

    const first = result.current.forSubject(body);
    result.current.done();

    expect(result.current.forSubject(body)).not.toBe(first);
  });
});
