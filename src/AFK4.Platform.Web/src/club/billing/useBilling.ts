import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '../../api/clubApi';
import type { Invoice, TenantSubscription } from '../../api/types';

export type BillingState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; subscription: TenantSubscription; invoices: Invoice[]; retry: () => void };

export function useBilling(client: ClubApiClient, organizationId: string): BillingState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [subscription, setSubscription] = useState<TenantSubscription | null>(null);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    Promise.all([
      clientRef.current.getSubscription(organizationId),
      clientRef.current.listInvoices(organizationId)
    ])
      .then(([sub, inv]) => {
        if (cancelled) return;
        setSubscription(sub);
        setInvoices(inv);
        setPhase('ready');
      })
      .catch(() => {
        if (!cancelled) setPhase('error');
      });
    return () => { cancelled = true; };
  }, [organizationId, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || subscription === null) return { status: 'loading' };
  return { status: 'ready', subscription, invoices, retry };
}
