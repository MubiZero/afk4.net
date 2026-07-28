import { useEffect } from 'react';

export interface HotkeyBinding {
  /** Single key, compared case-insensitively (e.g. 'k', 'z'). */
  key: string;
  ctrl?: boolean;
  alt?: boolean;
  shift?: boolean;
  meta?: boolean;
  onTrigger: (e: KeyboardEvent) => void;
  /** When false (default), the binding is skipped while focus is in an editable element. */
  allowInInputs?: boolean;
}

function isEditableTarget(target: EventTarget | null): boolean {
  const el = target as HTMLElement | null;
  if (!el) return false;
  if (el.isContentEditable) return true;
  const tag = el.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
}

/**
 * Registers a single global keydown listener over an array of bindings.
 *
 * Callers should pass a stable/memoized `bindings` array, otherwise the listener
 * is re-attached on every render.
 */
export function useHotkeys(bindings: HotkeyBinding[]): void {
  useEffect(() => {
    function handle(e: KeyboardEvent) {
      if (e.repeat) return;

      for (const binding of bindings) {
        if (e.key.toLowerCase() !== binding.key.toLowerCase()) continue;
        if (Boolean(binding.ctrl) !== e.ctrlKey) continue;
        if (Boolean(binding.alt) !== e.altKey) continue;
        if (Boolean(binding.shift) !== e.shiftKey) continue;
        if (Boolean(binding.meta) !== e.metaKey) continue;
        if (!binding.allowInInputs && isEditableTarget(e.target)) continue;

        e.preventDefault();
        binding.onTrigger(e);
      }
    }

    window.addEventListener('keydown', handle);
    return () => window.removeEventListener('keydown', handle);
  }, [bindings]);
}
