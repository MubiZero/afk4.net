import { useI18n } from '@afk4/i18n';
import { ArrowRight, CircleDollarSign, History, ReceiptText, SlidersHorizontal } from 'lucide-react';
import type { LedgerEntryDto } from '../operatorApiClients';
import { formatMinorUnits } from '../operatorHelpers';
import { projectLedgerEntry } from './playersModel';

// Кошелёк = зона ДЕЙСТВИЙ слева (баланс/долг показывает сводка-чипы, здесь не дублируем) +
// мини-лента последних операций справа (read-only, со ссылкой на полную вкладку «История»).
// Пополнение — основное действие; форма погашения появляется только при наличии долга.
// feedback показывается глобально в оркестраторе — единый источник, здесь не дублируем.
export function WalletSection({
  debtMinorUnits,
  currencyCode,
  topUpAmount,
  topUpReason,
  debtAmount,
  debtReason,
  canTopUp,
  canPayDebt,
  canCorrect,
  recentEntries,
  onShowHistory,
  onChangeTopUpAmount,
  onChangeTopUpReason,
  onChangeDebtAmount,
  onChangeDebtReason,
  onTopUp,
  onPayDebt,
  onCorrect,
}: {
  debtMinorUnits: number;
  currencyCode: string;
  topUpAmount: string;
  topUpReason: string;
  debtAmount: string;
  debtReason: string;
  canTopUp: boolean;
  canPayDebt: boolean;
  canCorrect: boolean;
  recentEntries: LedgerEntryDto[];
  onShowHistory: () => void;
  onChangeTopUpAmount: (value: string) => void;
  onChangeTopUpReason: (value: string) => void;
  onChangeDebtAmount: (value: string) => void;
  onChangeDebtReason: (value: string) => void;
  onTopUp: () => void;
  onPayDebt: () => void;
  onCorrect: () => void;
}) {
  const { t } = useI18n();
  const hasDebt = debtMinorUnits > 0;

  return (
    <div className="clients-wallet-layout">
      <div className="clients-wallet-actions">
        <form
          className="clients-wallet-form"
          onSubmit={(event) => {
            event.preventDefault();
            onTopUp();
          }}
        >
          <strong className="clients-section-title">{t('op.players.wallet.topUpTitle')}</strong>
          <label htmlFor="wallet-topup-amount">{t('op.players.actions.topUpAmountLabel')}</label>
          <input
            id="wallet-topup-amount"
            inputMode="decimal"
            value={topUpAmount}
            disabled={!canTopUp}
            onChange={(event) => onChangeTopUpAmount(event.currentTarget.value)}
          />
          <label htmlFor="wallet-topup-reason">{t('op.players.actions.topUpReasonLabel')}</label>
          <input
            id="wallet-topup-reason"
            value={topUpReason}
            disabled={!canTopUp}
            onChange={(event) => onChangeTopUpReason(event.currentTarget.value)}
          />
          <button type="submit" className="clients-primary-action" disabled={!canTopUp}>
            <CircleDollarSign size={15} aria-hidden="true" />
            {t('op.players.actions.topUpBtn')}
          </button>
        </form>

        {hasDebt && (
          <form
            className="clients-wallet-form clients-wallet-form-debt"
            onSubmit={(event) => {
              event.preventDefault();
              onPayDebt();
            }}
          >
            <strong className="clients-section-title">{t('op.players.wallet.payDebtTitle')}</strong>
            <label htmlFor="wallet-debt-amount">{t('op.players.actions.debtAmountLabel')}</label>
            <input
              id="wallet-debt-amount"
              inputMode="decimal"
              value={debtAmount}
              disabled={!canPayDebt}
              onChange={(event) => onChangeDebtAmount(event.currentTarget.value)}
            />
            <label htmlFor="wallet-debt-reason">{t('op.players.actions.debtReasonLabel')}</label>
            <input
              id="wallet-debt-reason"
              value={debtReason}
              disabled={!canPayDebt}
              onChange={(event) => onChangeDebtReason(event.currentTarget.value)}
            />
            <button type="submit" className="clients-primary-action clients-debt-action" disabled={!canPayDebt}>
              <ReceiptText size={15} aria-hidden="true" />
              {t('op.players.actions.writeOffDebtBtn')}
            </button>
          </form>
        )}

        {canCorrect && (
          <button type="button" className="clients-wallet-correction-link" onClick={onCorrect}>
            <SlidersHorizontal size={14} aria-hidden="true" />
            {t('op.players.correction.openLink')}
          </button>
        )}
      </div>

      <aside className="clients-wallet-recent">
        <div className="clients-wallet-recent-head">
          <History size={14} aria-hidden="true" />
          <strong>{t('op.players.wallet.recentTitle')}</strong>
        </div>
        {recentEntries.length === 0 ? (
          <p className="clients-wallet-recent-empty">{t('op.players.wallet.recentEmpty')}</p>
        ) : (
          <ul className="clients-wallet-recent-list">
            {recentEntries.map((raw) => {
              const view = projectLedgerEntry(raw, t);
              const sign = view.isCredit ? '+' : '−';
              const amount = formatMinorUnits(Math.abs(view.amountMinorUnits), view.currencyCode || currencyCode);
              return (
                <li key={view.id} className={`clients-wallet-recent-row ${view.isCredit ? 'is-credit' : 'is-debit'}`}>
                  <span className="clients-wallet-recent-time">{view.timeLabel}</span>
                  <span className="clients-wallet-recent-type">{view.typeLabel}</span>
                  <b className="clients-wallet-recent-amount">{sign}{amount}</b>
                </li>
              );
            })}
          </ul>
        )}
        <button type="button" className="clients-wallet-recent-link" onClick={onShowHistory}>
          {t('op.players.wallet.allHistory')}
          <ArrowRight size={13} aria-hidden="true" />
        </button>
      </aside>
    </div>
  );
}
