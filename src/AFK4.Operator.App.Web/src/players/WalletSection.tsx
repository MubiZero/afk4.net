import { useI18n } from '@afk4/i18n';
import { ArrowRight, CircleDollarSign, History, ReceiptText, SlidersHorizontal } from 'lucide-react';
import type { LedgerEntryDto } from '../operatorApiClients';
import { LedgerRow } from './LedgerRow';
import { projectLedgerEntry } from './playersModel';

// Кошелёк = зона ДЕЙСТВИЙ слева (баланс/долг показывает сводка-чипы, здесь не дублируем) +
// мини-лента последних операций справа (read-only, со ссылкой на полную вкладку «История»).
// На широком экране (showRecent=false) мини-лента не нужна: её заменяет постоянный
// правый рейл с полным журналом — здесь остаются только действия одной колонкой.
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
  showRecent = true,
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
  showRecent?: boolean;
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
    <div className={`clients-wallet-layout${showRecent ? '' : ' is-solo'}`}>
      <div className="clients-wallet-actions">
        <form
          className="clients-wallet-form"
          onSubmit={(event) => {
            event.preventDefault();
            onTopUp();
          }}
        >
          <strong className="clients-section-title">{t('op.players.wallet.topUpTitle')}</strong>
          <div className="clients-wallet-fields">
            <div className="ui-field">
              <label htmlFor="wallet-topup-amount">{t('op.players.actions.topUpAmountLabel')}</label>
              <input
                id="wallet-topup-amount"
                inputMode="decimal"
                placeholder="0.00"
                value={topUpAmount}
                disabled={!canTopUp}
                onChange={(event) => onChangeTopUpAmount(event.currentTarget.value)}
              />
            </div>
            <div className="ui-field">
              <label htmlFor="wallet-topup-reason">{t('op.players.actions.topUpReasonLabel')}</label>
              <input
                id="wallet-topup-reason"
                placeholder={t('op.players.actions.topUpDefault')}
                value={topUpReason}
                disabled={!canTopUp}
                onChange={(event) => onChangeTopUpReason(event.currentTarget.value)}
              />
            </div>
          </div>
          <button type="submit" className="ui-btn ui-btn--primary ui-btn--block" disabled={!canTopUp}>
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
            <div className="clients-wallet-fields">
              <div className="ui-field">
                <label htmlFor="wallet-debt-amount">{t('op.players.actions.debtAmountLabel')}</label>
                <input
                  id="wallet-debt-amount"
                  inputMode="decimal"
                  placeholder="0.00"
                  value={debtAmount}
                  disabled={!canPayDebt}
                  onChange={(event) => onChangeDebtAmount(event.currentTarget.value)}
                />
              </div>
              <div className="ui-field">
                <label htmlFor="wallet-debt-reason">{t('op.players.actions.debtReasonLabel')}</label>
                <input
                  id="wallet-debt-reason"
                  placeholder={t('op.players.actions.writeOffDebtDefault')}
                  value={debtReason}
                  disabled={!canPayDebt}
                  onChange={(event) => onChangeDebtReason(event.currentTarget.value)}
                />
              </div>
            </div>
            <button type="submit" className="ui-btn ui-btn--danger ui-btn--block" disabled={!canPayDebt}>
              <ReceiptText size={15} aria-hidden="true" />
              {t('op.players.actions.writeOffDebtBtn')}
            </button>
          </form>
        )}

        {canCorrect && (
          <button type="button" className="ui-btn ui-btn--ghost ui-btn--sm" onClick={onCorrect}>
            <SlidersHorizontal size={14} aria-hidden="true" />
            {t('op.players.correction.openLink')}
          </button>
        )}
      </div>

      {showRecent && (
      <aside className="clients-wallet-recent">
        <div className="clients-wallet-recent-head">
          <History size={14} aria-hidden="true" />
          <strong>{t('op.players.wallet.recentTitle')}</strong>
        </div>
        {recentEntries.length === 0 ? (
          <p className="clients-wallet-recent-empty">{t('op.players.wallet.recentEmpty')}</p>
        ) : (
          <div className="clients-wallet-recent-list ui-ledger-list">
            {recentEntries.map((raw) => {
              const view = projectLedgerEntry(raw, t);
              return <LedgerRow key={view.id} view={view} currencyCode={currencyCode} compact />;
            })}
          </div>
        )}
        <button type="button" className="clients-wallet-recent-link" onClick={onShowHistory}>
          {t('op.players.wallet.allHistory')}
          <ArrowRight size={13} aria-hidden="true" />
        </button>
      </aside>
      )}
    </div>
  );
}
