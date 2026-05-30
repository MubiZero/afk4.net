import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import type { PlayerRow } from './clientsModel';
import { WalletPanel, type MoneyPerms } from './WalletPanel';
import { PackagesPanel } from './PackagesPanel';

type Client = Pick<ClubApiClient,
  'getWalletSummary' | 'topUpWallet' | 'payDebt' | 'createManualCorrection' | 'refundLedgerEntry'
  | 'getPlayerPackages' | 'getPackageOptions' | 'purchasePackage'>;

export function ClientDetail({ client, player, branchId, organizationId, canViewBilling, moneyPerms, canPurchase, onMutated }: {
  client: Client;
  player: PlayerRow;
  branchId: string;
  organizationId: string;
  canViewBilling: boolean;
  moneyPerms?: MoneyPerms;
  canPurchase?: boolean;
  onMutated?: () => void;
}) {
  const { t } = useI18n();
  return (
    <Card className="flex flex-col gap-4 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-lg font-semibold">{player.displayName}</p>
          <p className="text-sm text-muted-foreground">{player.phone === '' ? '—' : player.phone}</p>
        </div>
        <Badge variant={player.isActive ? 'default' : 'secondary'}>
          {player.isActive ? t('clients.status.active') : t('clients.status.inactive')}
        </Badge>
      </div>

      {canViewBilling ? (
        <>
          <WalletPanel
            client={client} playerAccountId={player.playerAccountId} organizationId={organizationId}
            moneyPerms={moneyPerms} onMutated={onMutated}
          />
          <PackagesPanel
            client={client} playerAccountId={player.playerAccountId} branchId={branchId} organizationId={organizationId}
            canPurchase={canPurchase ?? false} onMutated={onMutated}
          />
        </>
      ) : (
        <p className="text-sm text-muted-foreground">{t('clients.billing.noAccess')}</p>
      )}

      <p className="text-xs text-muted-foreground">{t('clients.editUnavailable')}</p>
    </Card>
  );
}
