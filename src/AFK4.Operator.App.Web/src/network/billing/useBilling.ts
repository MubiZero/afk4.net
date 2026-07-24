import { useCallback, useEffect, useRef, useState } from 'react';
import type { InvoiceDto, TenantSubscriptionDto } from '../../api/clients/orgBilling';

export interface BillingClient {
  getSubscription(organizationId: string): Promise<TenantSubscriptionDto>;
  listInvoices(organizationId: string): Promise<InvoiceDto[]>;
}

export type BillingState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; subscription: TenantSubscriptionDto; invoices: InvoiceDto[]; retry: () => void };

// Loads the subscription + invoice list for the active org in parallel. Both calls are read-only
// (Task 6 is a read-only screen by design — no plan-management actions), so a single combined
// error state is enough: there's nothing partial to render if either call fails.
export function useBilling(client: BillingClient, organizationId: string): BillingState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [subscription, setSubscription] = useState<TenantSubscriptionDto | null>(null);
  const [invoices, setInvoices] = useState<InvoiceDto[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;
  const retry = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    Promise.all([clientRef.current.getSubscription(organizationId), clientRef.current.listInvoices(organizationId)])
      .then(([sub, inv]) => {
        if (!cancelled) {
          setSubscription(sub);
          setInvoices(inv);
          setPhase('ready');
        }
      })
      .catch(() => {
        if (!cancelled) setPhase('error');
      });
    return () => {
      cancelled = true;
    };
  }, [organizationId, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || subscription === null) return { status: 'loading' };
  return { status: 'ready', subscription, invoices, retry };
}
