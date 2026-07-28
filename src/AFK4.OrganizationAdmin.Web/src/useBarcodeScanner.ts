import { useEffect, useRef } from 'react';
import { feedScanner, EMPTY_SCANNER, type ScannerOptions, type ScannerState } from './barcodeScanner';

export function useBarcodeScanner(enabled: boolean, onScan: (code: string) => void, opts?: ScannerOptions): void {
  const stateRef = useRef<ScannerState>(EMPTY_SCANNER);
  const onScanRef = useRef(onScan);
  onScanRef.current = onScan;

  useEffect(() => {
    if (!enabled) { stateRef.current = EMPTY_SCANNER; return; }
    function handle(e: KeyboardEvent) {
      if (e.ctrlKey || e.metaKey || e.altKey) return;
      const step = feedScanner(stateRef.current, e.key, performance.now(), opts);
      stateRef.current = step.state;
      if (step.capture) e.preventDefault();
      if (step.scanned) onScanRef.current(step.scanned);
    }
    window.addEventListener('keydown', handle, true); // capture-фаза: перехватить до полей
    return () => window.removeEventListener('keydown', handle, true);
  }, [enabled, opts]);
}
