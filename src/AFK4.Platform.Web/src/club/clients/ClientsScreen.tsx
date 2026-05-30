import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useClientSearch } from './useClientSearch';
import { CreateClientDialog } from './CreateClientDialog';
import { ClientDetail } from './ClientDetail';
import type { PlayerRow } from './clientsModel';
import type { MoneyPerms } from './WalletPanel';

type Client = Pick<ClubApiClient,
  'searchPlayers' | 'getWalletSummary' | 'createPlayer'
  | 'topUpWallet' | 'payDebt' | 'createManualCorrection' | 'refundLedgerEntry'>;

export function ClientsScreen({ client, branchId, organizationId, canCreate, canViewBilling, moneyPerms }: {
  client: Client;
  branchId: string;
  organizationId: string;
  canCreate: boolean;
  canViewBilling: boolean;
  moneyPerms?: MoneyPerms;
}) {
  const { t, formatNumber } = useI18n();
  const [query, setQuery] = useState('');
  const [selected, setSelected] = useState<PlayerRow | null>(null);
  const [creating, setCreating] = useState(false);
  const state = useClientSearch(client, branchId, query);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-2">
        <Input
          aria-label={t('clients.search.label')}
          placeholder={t('clients.search.placeholder')}
          value={query}
          onChange={e => setQuery(e.target.value)}
        />
        {canCreate && <Button onClick={() => setCreating(true)}>{t('clients.create')}</Button>}
      </div>

      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
      ) : state.rows.length === 0 ? (
        <EmptyState message={t('clients.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('clients.col.name')}</TableHead>
              <TableHead>{t('clients.col.phone')}</TableHead>
              <TableHead>{t('clients.col.wallet')}</TableHead>
              <TableHead>{t('clients.col.debt')}</TableHead>
              <TableHead>{t('clients.col.packages')}</TableHead>
              <TableHead>{t('clients.col.status')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {state.rows.map(row => (
              <TableRow key={row.playerAccountId} data-clickable="true" onClick={() => setSelected(row)}>
                <TableCell className="font-medium">{row.displayName}</TableCell>
                <TableCell>{row.phone === '' ? '—' : row.phone}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.walletMajor)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.debtMajor)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.activePackageCount)}</TableCell>
                <TableCell>
                  <Badge variant={row.isActive ? 'default' : 'secondary'}>
                    {row.isActive ? t('clients.status.active') : t('clients.status.inactive')}
                  </Badge>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {selected !== null ? (
        <ClientDetail
          key={selected.playerAccountId}
          client={client}
          player={selected}
          organizationId={organizationId}
          canViewBilling={canViewBilling}
          moneyPerms={moneyPerms}
          onMutated={() => { if (state.status === 'ready') state.retry(); }}
        />
      ) : (
        <p className="text-sm text-muted-foreground">{t('clients.selectHint')}</p>
      )}

      {creating && (
        <CreateClientDialog
          open branchId={branchId} organizationId={organizationId} client={client}
          onOpenChange={o => { if (!o) setCreating(false); }}
          onDone={() => state.status === 'ready' && state.retry()}
        />
      )}
    </div>
  );
}
