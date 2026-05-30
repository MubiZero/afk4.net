import { useCallback, useEffect, useRef, useState } from 'react';

export type ReportState<T> =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; data: T; retry: () => void };

export function useReport<T>(loader: () => Promise<T>, deps: readonly unknown[]): ReportState<T> {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [data, setData] = useState<T | null>(null);
  const loaderRef = useRef(loader);
  loaderRef.current = loader;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    loaderRef.current()
      .then(result => { if (!cancelled) { setData(result); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || data === null) return { status: 'loading' };
  return { status: 'ready', data, retry };
}
