import { useCallback, useEffect, useRef, useState } from 'react';
import type { ReportFormatters, ReportView } from './reportModel';

export type ReportState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; view: ReportView; retry: () => void };

export function useReport(
  load: () => Promise<Record<string, unknown>>,
  build: (result: Record<string, unknown>, fmt: ReportFormatters) => ReportView,
  fmt: ReportFormatters,
  deps: readonly unknown[]
): ReportState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [view, setView] = useState<ReportView | null>(null);
  const loadRef = useRef(load);
  loadRef.current = load;
  const buildRef = useRef(build);
  buildRef.current = build;
  const fmtRef = useRef(fmt);
  fmtRef.current = fmt;
  const retry = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    loadRef.current()
      .then((result) => {
        if (!cancelled) {
          setView(buildRef.current(result, fmtRef.current));
          setPhase('ready');
        }
      })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || view === null) return { status: 'loading' };
  return { status: 'ready', view, retry };
}
