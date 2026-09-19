import type { InvoicesApi } from '@/api/platformClients/invoices';
import type { InvoiceListItem } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

export type InvoicesState = Loadable<InvoiceListItem[]>;

type Client = Pick<InvoicesApi, 'listInvoices'>;

export function useInvoices(client: Client): InvoicesState {
  return useLoadable(() => client.listInvoices());
}
