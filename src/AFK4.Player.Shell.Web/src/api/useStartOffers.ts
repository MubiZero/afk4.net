import { useCallback, useEffect, useRef, useState } from 'react';
import type { PlayerStartOffersDto } from '@afk4/contracts';
import { getJson } from './playerApi';

export interface StartOffersView {
  offers: PlayerStartOffersDto | null;
  failed: boolean;
  loading: boolean;
  /** Перечитать тихо: показанное остаётся на экране, пока не придёт новое. */
  reload: () => void;
}

/** Цены для этого ПК с готовыми суммами (спека, §5.5): считает сервер, экран только показывает. */
export function useStartOffers(baseUrl: string | null): StartOffersView {
  const [offers, setOffers] = useState<PlayerStartOffersDto | null>(null);
  const [failed, setFailed] = useState(false);
  const [loading, setLoading] = useState(true);
  const current = useRef<AbortController | null>(null);

  const load = useCallback(async () => {
    current.current?.abort();
    if (!baseUrl) {
      setFailed(true);
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    current.current = controller;
    setLoading(true);
    try {
      const next = await getJson<PlayerStartOffersDto>(baseUrl, '/api/me/this-pc/start-offers', controller.signal);
      if (controller.signal.aborted) return;
      setOffers(next);
      setFailed(false);
    } catch {
      if (controller.signal.aborted) return;
      setFailed(true);
    } finally {
      if (!controller.signal.aborted) setLoading(false);
    }
  }, [baseUrl]);

  useEffect(() => {
    void load();
    return () => current.current?.abort();
  }, [load]);

  return { offers, failed, loading, reload: () => void load() };
}
