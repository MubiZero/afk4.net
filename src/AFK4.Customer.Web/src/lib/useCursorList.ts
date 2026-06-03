import { useCallback, useEffect, useRef, useState } from 'react';
import type { CursorPage } from '@/api/types';

export type CursorListState<T> =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | {
      status: 'ready';
      items: T[];
      hasMore: boolean;
      loadingMore: boolean;
      loadMore: () => void;
      retry: () => void;
    };

// Generic forward-only cursor pagination. First page loads on mount; loadMore()
// appends. fetchPage is read from a ref so a screen can pass an inline lambda
// without retriggering the initial load on every render.
export function useCursorList<T>(fetchPage: (cursor?: string) => Promise<CursorPage<T>>): CursorListState<T> {
  const fetchRef = useRef(fetchPage);
  fetchRef.current = fetchPage;

  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [items, setItems] = useState<T[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);
  const [reloadTick, setReloadTick] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    fetchRef.current()
      .then((page) => {
        if (cancelled) return;
        setItems(page.items);
        setCursor(page.nextCursor);
        setPhase('ready');
      })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [reloadTick]);

  const retry = useCallback(() => setReloadTick((tick) => tick + 1), []);

  const loadMore = useCallback(() => {
    if (cursor === null) return;
    setLoadingMore(true);
    fetchRef.current(cursor)
      .then((page) => {
        setItems((prev) => [...prev, ...page.items]);
        setCursor(page.nextCursor);
      })
      .finally(() => setLoadingMore(false));
  }, [cursor]);

  if (phase === 'loading') return { status: 'loading' };
  if (phase === 'error') return { status: 'error', retry };
  return { status: 'ready', items, hasMore: cursor !== null, loadingMore, loadMore, retry };
}
