// useUnsavedGuard.test.ts
import { describe, it, expect, mock } from 'bun:test';
import { renderHook, act } from '@testing-library/react';
import { useUnsavedGuard } from './useUnsavedGuard';

describe('useUnsavedGuard', () => {
  it('navigates immediately when not dirty', () => {
    const onNavigate = mock(() => {});
    const { result } = renderHook(() => useUnsavedGuard({ isDirty: false, onNavigate, onDiscard: () => {} }));
    act(() => result.current.requestNavigate('news'));
    expect(onNavigate).toHaveBeenCalledWith('news');
    expect(result.current.pendingTarget).toBeNull();
  });

  it('blocks and stores the target when dirty, then confirm proceeds', () => {
    const onNavigate = mock(() => {});
    const onDiscard = mock(() => {});
    const { result } = renderHook(() => useUnsavedGuard({ isDirty: true, onNavigate, onDiscard }));
    act(() => result.current.requestNavigate('payments'));
    expect(onNavigate).not.toHaveBeenCalled();
    expect(result.current.pendingTarget).toBe('payments');
    act(() => result.current.confirm());
    expect(onDiscard).toHaveBeenCalledTimes(1);
    expect(onNavigate).toHaveBeenCalledWith('payments');
    expect(result.current.pendingTarget).toBeNull();
  });

  it('cancel clears the pending target without navigating', () => {
    const onNavigate = mock(() => {});
    const { result } = renderHook(() => useUnsavedGuard({ isDirty: true, onNavigate, onDiscard: () => {} }));
    act(() => result.current.requestNavigate('club'));
    act(() => result.current.cancel());
    expect(result.current.pendingTarget).toBeNull();
    expect(onNavigate).not.toHaveBeenCalled();
  });
});
