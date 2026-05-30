import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';
import type { ClubApiClient } from '@/api/clubApi';
import { useWalletSummary } from './useWalletSummary';

type Client = Pick<ClubApiClient, 'getWalletSummary'>;

const ENTRY_TYPE_KEY: Record<string, MessageKey> = {
  top_up: 'ledger.type.top_up',
  gameplay_charge: 'ledger.type.gameplay_charge',
  package_purchase: 'ledger.type.package_purchase',
  package_consumption: 'ledger.type.package_consumption',
  bonus_grant: 'ledger.type.bonus_grant',
  bonus_consumption: 'ledger.type.bonus_consumption',
  refund: 'ledger.type.refund',
  manual_correction: 'ledger.type.manual_correction',
  postpaid_debt: 'ledger.type.postpaid_debt',
  debt_payment: 'ledger.type.debt_payment',
  reversal: 'ledger.type.reversal'
};

const ACCOUNT_TYPE_KEY: Record<string, MessageKey> = {
  wallet: 'ledger.account.wallet',
  debt: 'ledger.account.debt',
  package_time: 'ledger.account.package_time',
  bonus_time: 'ledger.account.bonus_time'
};

export function WalletPanel({ client, playerAccountId }: { client: Client; playerAccountId: string }) {
  const { t, formatCurrency, formatNumber, formatDate } = useI18n();
  const state = useWalletSummary(client, playerAccountId);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { balance, ledger } = state;
  const entryLabel = (type: string): string => (ENTRY_TYPE_KEY[type] ? t(ENTRY_TYPE_KEY[type]) : type);
  const accountLabel = (type: string): string => (ACCOUNT_TYPE_KEY[type] ? t(ACCOUNT_TYPE_KEY[type]) : type);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-6">
        <div>
          <p className="text-xs text-muted-foreground">{t('clients.balance.wallet')}</p>
          <p className="text-lg font-semibold tabular-nums">{formatCurrency(balance.walletMajor, balance.walletCurrency)}</p>
        </div>
        <div>
          <p className="text-xs text-muted-foreground">{t('clients.balance.debt')}</p>
          <p className="text-lg font-semibold tabular-nums">{formatCurrency(balance.debtMajor, balance.debtCurrency)}</p>
        </div>
      </div>

      <div>
        <h3 className="mb-2 text-sm font-medium">{t('clients.history.title')}</h3>
        {ledger.length === 0 ? (
          <EmptyState message={t('clients.history.empty')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('clients.history.col.date')}</TableHead>
                <TableHead>{t('clients.history.col.type')}</TableHead>
                <TableHead>{t('clients.history.col.account')}</TableHead>
                <TableHead>{t('clients.history.col.amount')}</TableHead>
                <TableHead>{t('clients.history.col.minutes')}</TableHead>
                <TableHead>{t('clients.history.col.reason')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {ledger.map(row => (
                <TableRow key={row.ledgerEntryId}>
                  <TableCell>{formatDate(row.createdAtUtc)}</TableCell>
                  <TableCell>{entryLabel(row.entryType)}</TableCell>
                  <TableCell>{accountLabel(row.accountType)}</TableCell>
                  <TableCell className="tabular-nums">{formatCurrency(row.amountMajor, row.currencyCode)}</TableCell>
                  <TableCell className="tabular-nums">{row.quantityMinutes === 0 ? '—' : formatNumber(row.quantityMinutes)}</TableCell>
                  <TableCell>{row.reason}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
        <p className="mt-2 text-xs text-muted-foreground">{t('clients.history.note')}</p>
      </div>
    </div>
  );
}
