import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PackagesApi } from '@/api/clients/packages';
import { usePackages } from './usePackages';
import { PackageFormDialog } from './PackageFormDialog';
import type { PackageRow } from './packagesModel';

type Client = Pick<PackagesApi, 'getPackageOptions' | 'createPackageDefinition' | 'updatePackageDefinition'>;

export function PackagesTab({ client, branchId, organizationId, canManage }: {
  client: Client;
  branchId: string;
  organizationId: string;
  canManage: boolean;
}) {
  const { t, formatNumber, formatCurrency } = useI18n();
  const state = usePackages(client, branchId);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<PackageRow | null>(null);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { rows, retry } = state;

  return (
    <div className="flex flex-col gap-4">
      {canManage && (
        <div className="flex justify-end">
          <Button onClick={() => setCreating(true)}>{t('loyalty.create')}</Button>
        </div>
      )}

      {rows.length === 0 ? (
        <EmptyState message={t('loyalty.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('loyalty.col.name')}</TableHead>
              <TableHead>{t('loyalty.col.price')}</TableHead>
              <TableHead>{t('loyalty.col.included')}</TableHead>
              <TableHead>{t('loyalty.col.bonus')}</TableHead>
              <TableHead>{t('loyalty.col.expires')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map(row => (
              <TableRow key={row.packageDefinitionId} data-clickable={canManage ? 'true' : undefined}
                onClick={canManage ? () => setEditing(row) : undefined}>
                <TableCell className="font-medium">{row.name}</TableCell>
                <TableCell className="tabular-nums">{formatCurrency(row.price, row.currencyCode)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.includedMinutes)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.bonusMinutes)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.expiresAfterDays)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <p className="text-xs text-muted-foreground">{t('loyalty.activeOnlyNote')}</p>

      {creating && (
        <PackageFormDialog
          open mode="create" branchId={branchId} organizationId={organizationId} client={client}
          onOpenChange={o => { if (!o) setCreating(false); }}
          onDone={() => retry()}
        />
      )}
      {editing !== null && (
        <PackageFormDialog
          key={editing.packageDefinitionId}
          open mode="edit" branchId={branchId} organizationId={organizationId} client={client} initial={editing}
          onOpenChange={o => { if (!o) setEditing(null); }}
          onDone={() => retry()}
        />
      )}
    </div>
  );
}
