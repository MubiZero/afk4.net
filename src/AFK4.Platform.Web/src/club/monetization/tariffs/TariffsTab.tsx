import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useTariffs } from './useTariffs';
import { TariffFormDialog } from './TariffFormDialog';
import type { TariffRow } from './tariffsModel';

type Client = Pick<ClubApiClient,
  'getTariffOptions' | 'createTariff' | 'createTariffVersion' | 'updateTariff' | 'updateTariffVersion'>;

export function TariffsTab({ client, branchId, organizationId, canManage }: {
  client: Client;
  branchId: string;
  organizationId: string;
  canManage: boolean;
}) {
  const { t, formatNumber, formatCurrency } = useI18n();
  const state = useTariffs(client, branchId);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<TariffRow | null>(null);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { rows, retry } = state;

  return (
    <div className="flex flex-col gap-4">
      {canManage && (
        <div className="flex justify-end">
          <Button onClick={() => setCreating(true)}>{t('tariffs.create')}</Button>
        </div>
      )}

      {rows.length === 0 ? (
        <EmptyState message={t('tariffs.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('tariffs.col.name')}</TableHead>
              <TableHead>{t('tariffs.col.price')}</TableHead>
              <TableHead>{t('tariffs.col.minMinutes')}</TableHead>
              <TableHead>{t('tariffs.col.rounding')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map(row => (
              <TableRow key={row.tariffId} data-clickable={canManage ? 'true' : undefined}
                onClick={canManage ? () => setEditing(row) : undefined}>
                <TableCell className="font-medium">{row.name}</TableCell>
                <TableCell className="tabular-nums">{formatCurrency(row.pricePerMinute, row.currencyCode)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.minimumBillableMinutes)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.roundingIncrementMinutes)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <p className="text-xs text-muted-foreground">{t('tariffs.activeOnlyNote')}</p>

      {creating && (
        <TariffFormDialog
          open mode="create" branchId={branchId} organizationId={organizationId} client={client}
          onOpenChange={o => { if (!o) setCreating(false); }}
          onDone={() => retry()}
        />
      )}
      {editing !== null && (
        <TariffFormDialog
          key={editing.tariffVersionId}
          open mode="edit" branchId={branchId} organizationId={organizationId} client={client} initial={editing}
          onOpenChange={o => { if (!o) setEditing(null); }}
          onDone={() => retry()}
        />
      )}
    </div>
  );
}
