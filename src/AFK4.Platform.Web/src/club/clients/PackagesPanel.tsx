import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlayersApi } from '@/api/clients/players';
import type { PackagesApi } from '@/api/clients/packages';
import { usePlayerPackages } from './usePlayerPackages';
import { PurchasePackageDialog } from './PurchasePackageDialog';

type Client = Pick<PlayersApi, 'getPlayerPackages' | 'purchasePackage'>;

export function PackagesPanel({ client, packages, playerAccountId, branchId, organizationId, canPurchase, onMutated }: {
  client: Client;
  packages: Pick<PackagesApi, 'getPackageOptions'>;
  playerAccountId: string;
  branchId: string;
  organizationId: string;
  canPurchase: boolean;
  onMutated?: () => void;
}) {
  const { t, formatNumber, formatDate } = useI18n();
  const state = usePlayerPackages({ players: client, packages }, playerAccountId, branchId);
  const [purchasing, setPurchasing] = useState(false);

  if (state.status === 'loading') return <LoadingCards count={1} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { rows, choices, retry } = state;

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-medium">{t('clientPackages.title')}</h3>
        {canPurchase && <Button size="sm" onClick={() => setPurchasing(true)}>{t('clientPackages.purchase')}</Button>}
      </div>

      {rows.length === 0 ? (
        <EmptyState message={t('clientPackages.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('clientPackages.col.name')}</TableHead>
              <TableHead>{t('clientPackages.col.included')}</TableHead>
              <TableHead>{t('clientPackages.col.bonus')}</TableHead>
              <TableHead>{t('clientPackages.col.expires')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map(row => (
              <TableRow key={row.playerPackageId}>
                <TableCell className="font-medium">{row.name}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.remainingIncludedMinutes)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.remainingBonusMinutes)}</TableCell>
                <TableCell>{row.expiresAtUtc === null ? t('clientPackages.noExpiry') : formatDate(row.expiresAtUtc)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {purchasing && (
        <PurchasePackageDialog
          open client={client} playerAccountId={playerAccountId} organizationId={organizationId} choices={choices}
          onOpenChange={o => { if (!o) setPurchasing(false); }}
          onDone={() => { retry(); onMutated?.(); }}
        />
      )}
    </div>
  );
}
