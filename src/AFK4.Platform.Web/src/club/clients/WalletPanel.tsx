import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';
import type { ClubApiClient } from '@/api/clubApi';
import { useWalletSummary } from './useWalletSummary';
import { AmountReasonDialog } from './AmountReasonDialog';
import { ManualCorrectionDialog } from './ManualCorrectionDialog';
import { RefundDialog, type RefundTarget } from './RefundDialog';

type Client = Pick<ClubApiClient,
  'getWalletSummary' | 'topUpWallet' | 'payDebt' | 'createManualCorrection' | 'refundLedgerEntry'>;

export interface MoneyPerms {
  topUp: boolean;
  payDebt: boolean;
  correct: boolean;
  refund: boolean;
}

const NO_PERMS: MoneyPerms = { topUp: false, payDebt: false, correct: false, refund: false };

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

export function WalletPanel({ client, playerAccountId, organizationId, moneyPerms = NO_PERMS, onMutated }: {
  client: Client;
  playerAccountId: string;
  organizationId: string;
  moneyPerms?: MoneyPerms;
  onMutated?: () => void;
}) {
  const { t, formatCurrency, formatNumber, formatDate } = useI18n();
  const state = useWalletSummary(client, playerAccountId);
  const [dialog, setDialog] = useState<'topUp' | 'payDebt' | 'correct' | null>(null);
  const [refundTarget, setRefundTarget] = useState<RefundTarget | null>(null);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { balance, ledger, retry } = state;
  const entryLabel = (type: string): string => (ENTRY_TYPE_KEY[type] ? t(ENTRY_TYPE_KEY[type]) : type);
  const accountLabel = (type: string): string => (ACCOUNT_TYPE_KEY[type] ? t(ACCOUNT_TYPE_KEY[type]) : type);
  const afterMutation = (): void => { retry(); onMutated?.(); };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end justify-between gap-4">
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
        <div className="flex flex-wrap gap-2">
          {moneyPerms.topUp && <Button size="sm" onClick={() => setDialog('topUp')}>{t('money.topUp')}</Button>}
          {moneyPerms.payDebt && <Button size="sm" variant="outline" onClick={() => setDialog('payDebt')}>{t('money.payDebt')}</Button>}
          {moneyPerms.correct && <Button size="sm" variant="outline" onClick={() => setDialog('correct')}>{t('money.correction')}</Button>}
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
                {moneyPerms.refund && <TableHead />}
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
                  {moneyPerms.refund && (
                    <TableCell>
                      <Button size="xs" variant="ghost" onClick={() => setRefundTarget({
                        ledgerEntryId: row.ledgerEntryId, amountMajor: row.amountMajor, currencyCode: row.currencyCode
                      })}>
                        {t('money.refund')}
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
        <p className="mt-2 text-xs text-muted-foreground">{t('clients.history.note')}</p>
      </div>

      {(dialog === 'topUp' || dialog === 'payDebt') && (
        <AmountReasonDialog
          open kind={dialog} client={client} playerAccountId={playerAccountId}
          organizationId={organizationId} currencyCode={balance.walletCurrency}
          onOpenChange={o => { if (!o) setDialog(null); }}
          onDone={afterMutation}
        />
      )}
      {dialog === 'correct' && (
        <ManualCorrectionDialog
          open client={client} playerAccountId={playerAccountId}
          organizationId={organizationId} currencyCode={balance.walletCurrency}
          onOpenChange={o => { if (!o) setDialog(null); }}
          onDone={afterMutation}
        />
      )}
      {refundTarget !== null && (
        <RefundDialog
          open client={client} playerAccountId={playerAccountId}
          organizationId={organizationId} entry={refundTarget}
          onOpenChange={o => { if (!o) setRefundTarget(null); }}
          onDone={afterMutation}
        />
      )}
    </div>
  );
}
