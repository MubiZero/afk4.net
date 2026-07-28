import { useCallback, useState } from 'react';
import type { ManagementDestinationId } from './managementNav';

export interface UnsavedGuard {
  pendingTarget: ManagementDestinationId | null; // set when a switch is blocked
  requestNavigate: (target: ManagementDestinationId) => void;
  confirm: () => void; // proceed to pendingTarget, clear dirty
  cancel: () => void; // stay
}

export function useUnsavedGuard(args: {
  isDirty: boolean;
  onNavigate: (target: ManagementDestinationId) => void;
  onDiscard: () => void; // clear the dirty state on the leaving destination
}): UnsavedGuard {
  const { isDirty, onNavigate, onDiscard } = args;
  const [pendingTarget, setPendingTarget] = useState<ManagementDestinationId | null>(null);

  const requestNavigate = useCallback(
    (target: ManagementDestinationId) => {
      if (!isDirty) {
        onNavigate(target);
        return;
      }
      setPendingTarget(target);
    },
    [isDirty, onNavigate]
  );

  const confirm = useCallback(() => {
    if (pendingTarget === null) return;
    onDiscard();
    onNavigate(pendingTarget);
    setPendingTarget(null);
  }, [pendingTarget, onDiscard, onNavigate]);

  const cancel = useCallback(() => {
    setPendingTarget(null);
  }, []);

  return { pendingTarget, requestNavigate, confirm, cancel };
}
