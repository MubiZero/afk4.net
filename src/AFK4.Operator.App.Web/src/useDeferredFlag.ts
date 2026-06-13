import { useEffect, useState } from 'react';

// Returns true only after `active` has stayed true for `delayMs`. A load that finishes before the
// delay never flips it, so a fast response doesn't flash a skeleton; it resets the moment `active`
// goes false.
export function useDeferredFlag(active: boolean, delayMs = 180): boolean {
  const [shown, setShown] = useState(false);

  useEffect(() => {
    if (!active) {
      setShown(false);
      return undefined;
    }
    const id = window.setTimeout(() => setShown(true), delayMs);
    return () => window.clearTimeout(id);
  }, [active, delayMs]);

  return shown;
}
