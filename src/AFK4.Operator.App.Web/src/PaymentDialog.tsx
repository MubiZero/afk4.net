import { useState, type ReactNode } from 'react';
import { Banknote, CircleDollarSign, Plus, ReceiptText, X } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import {
  buildCheckoutPayments,
  checkoutMethodLabel,
  formatCheckoutAmount,
  getAvailableCheckoutMethods,
  initialCheckoutDrafts,
  validateCheckoutPayments,
  type CheckoutMethod,
  type CheckoutPaymentDraft
} from './checkoutState';
import type { PaymentPartDto } from './operatorApiClients';
import { formatMinorUnits } from './operatorHelpers';

const checkoutMethodIcons: Record<CheckoutMethod, ReactNode> = {
  cash: <Banknote size={14} />,
  card_manual: <CircleDollarSign size={14} />,
  wallet: <ReceiptText size={14} />
};

// Способ оплаты как вкладка кассы. «Смешанно» — редкий сплит на несколько способов.
type PayMode = 'cash' | 'card' | 'deposit' | 'split';

// Быстрые купюры (номиналы сомони): один тап = «клиент дал столько», касса считает сдачу.
const CASH_DENOMINATIONS = [10, 20, 50, 100, 200] as const;

/** Одна позиция чека в окне оплаты (метка + сумма в minor units валюты счёта). */
export interface PaymentBillLine {
  label: string;
  amountMinorUnits: number;
}

/**
 * Тело окна приёма оплаты: один «счёт» (позиции + итог), выбор способа
 * (наличные с «получено»+сдачей / карта / депозит / смешанно) и подтверждение.
 * Чистая презентация поверх движка `checkoutState` — данные счёта приходят
 * пропсами, поэтому тело одинаково обслуживает расчёт сессии (Карта) и продажу
 * корзины (Касса). Модалку-обёртку (PanelModal) и состояния загрузки/ошибки
 * счёта держит вызывающая сторона — так диалог не пересоздаётся при загрузке.
 */
export function PaymentDialog({
  intro,
  contextLine,
  lines,
  dueLabel,
  grandTotalMinorUnits,
  currencyCode,
  walletBalanceMinorUnits,
  allowSplit,
  disabled,
  confirmVariant = 'danger',
  endWithoutPayment,
  onCancel,
  onConfirm
}: {
  intro?: string;
  contextLine?: string;
  lines: PaymentBillLine[];
  dueLabel: string;
  grandTotalMinorUnits: number;
  currencyCode: string;
  walletBalanceMinorUnits: number | null;
  allowSplit: boolean;
  disabled: boolean;
  confirmVariant?: 'danger' | 'accent';
  endWithoutPayment?: { label: string; onEnd: () => void };
  onCancel: () => void;
  onConfirm: (payments: PaymentPartDto[]) => void;
}) {
  const { t } = useI18n();
  const [payMode, setPayMode] = useState<PayMode>('cash');
  // Касса наличными: «получено» от клиента. Пусто = ровно по счёту (частый случай — ноль кликов).
  const [cashReceivedText, setCashReceivedText] = useState(() =>
    grandTotalMinorUnits > 0 ? formatCheckoutAmount(grandTotalMinorUnits) : '');
  // Сплит (вкладка «Смешанно») — отдельный редактируемый набор строк.
  const [splitDrafts, setSplitDrafts] = useState<CheckoutPaymentDraft[]>(() => initialCheckoutDrafts(grandTotalMinorUnits));

  const hasWallet = walletBalanceMinorUnits !== null && walletBalanceMinorUnits > 0;
  const walletBalance = walletBalanceMinorUnits;
  // Ноль к оплате со «завершить без оплаты» (сессия) — оплаты нет вовсе.
  const isZeroBill = grandTotalMinorUnits === 0 && Boolean(endWithoutPayment);

  // Депозит закрывает счёт в пределах баланса; остаток — наличными (точно, без сдачи).
  const depositCover = hasWallet ? Math.min(walletBalance ?? 0, grandTotalMinorUnits) : 0;
  const depositRemainder = grandTotalMinorUnits - depositCover;
  const depositDrafts: CheckoutPaymentDraft[] = depositRemainder > 0
    ? [{ method: 'wallet', amountText: formatCheckoutAmount(depositCover) }, { method: 'cash', amountText: formatCheckoutAmount(depositRemainder) }]
    : [{ method: 'wallet', amountText: formatCheckoutAmount(grandTotalMinorUnits) }];

  // Платёжные строки, которые реально уходят на бэк, зависят от выбранной вкладки.
  const drafts: CheckoutPaymentDraft[] = payMode === 'cash'
    ? [{ method: 'cash', amountText: cashReceivedText !== '' ? cashReceivedText : (grandTotalMinorUnits > 0 ? formatCheckoutAmount(grandTotalMinorUnits) : '') }]
    : payMode === 'card'
      ? [{ method: 'card_manual', amountText: formatCheckoutAmount(grandTotalMinorUnits) }]
      : payMode === 'deposit'
        ? depositDrafts
        : splitDrafts;

  const validation = validateCheckoutPayments(drafts, grandTotalMinorUnits, walletBalance, t);
  const canConfirm = !disabled && validation.canSubmit;

  const setReceivedMinor = (minorUnits: number) => setCashReceivedText(formatCheckoutAmount(minorUnits));
  const updateSplitDraft = (index: number, patch: Partial<CheckoutPaymentDraft>) => {
    setSplitDrafts((current) => current.map((draft, position) => (position === index ? { ...draft, ...patch } : draft)));
  };
  const addSplitDraft = () => setSplitDrafts((current) => {
    const nextMethod = getAvailableCheckoutMethods(current)[0];
    return nextMethod ? [...current, { method: nextMethod, amountText: '' }] : current;
  });
  const removeSplitDraft = (index: number) => {
    setSplitDrafts((current) => (current.length <= 1 ? current : current.filter((_, position) => position !== index)));
  };

  const payTabs: Array<{ id: PayMode; label: string }> = [
    { id: 'cash', label: t('op.checkout.method.cash') },
    { id: 'card', label: t('op.checkout.method.card') },
    ...(hasWallet ? [{ id: 'deposit' as PayMode, label: t('op.checkout.method.wallet') }] : []),
    ...(allowSplit ? [{ id: 'split' as PayMode, label: t('op.checkout.tab.split') }] : [])
  ];
  const canAddSplitMethod = getAvailableCheckoutMethods(splitDrafts).length > 0;

  return (
    <>
      {intro && <p className="checkout-subtitle">{intro}</p>}

      {/* Чек: позиции сверху, «К оплате» крупным итогом снизу — как настоящий чек кассы. */}
      <div className="checkout-receipt">
        {contextLine && <p className="checkout-context">{contextLine}</p>}
        {lines.map((line, index) => (
          <div className="checkout-receipt-line" key={`${line.label}-${index}`}>
            <span>{line.label}</span>
            <b>{formatMinorUnits(line.amountMinorUnits, currencyCode)}</b>
          </div>
        ))}
        <div className="checkout-receipt-total">
          <span>{dueLabel}</span>
          <strong>{formatMinorUnits(grandTotalMinorUnits, currencyCode)}</strong>
        </div>
      </div>

      {isZeroBill ? (
        <p className="checkout-no-payment">{t('op.checkout.noPayment')}</p>
      ) : (
        <div className="checkout-pay">
          <div className="checkout-tabs" role="tablist" aria-label={t('op.checkout.paymentMethod')}>
            {payTabs.map((tab) => (
              <button
                key={tab.id}
                type="button"
                role="tab"
                aria-selected={payMode === tab.id}
                className={payMode === tab.id ? 'active' : undefined}
                disabled={disabled}
                onClick={() => setPayMode(tab.id)}
              >
                {tab.label}
              </button>
            ))}
          </div>

          {payMode === 'cash' && (
            <div className="checkout-cash">
              <label className="checkout-received">
                <span>{t('op.checkout.cashTendered')}</span>
                <input
                  type="text"
                  inputMode="decimal"
                  value={cashReceivedText}
                  disabled={disabled}
                  placeholder={formatCheckoutAmount(grandTotalMinorUnits)}
                  onChange={(event) => setCashReceivedText(event.currentTarget.value)}
                />
              </label>
              <div className="checkout-cash-chips">
                <button type="button" disabled={disabled} onClick={() => setReceivedMinor(grandTotalMinorUnits)}>{t('op.checkout.exact')}</button>
                {CASH_DENOMINATIONS.map((denomination) => (
                  <button key={denomination} type="button" disabled={disabled} onClick={() => setReceivedMinor(denomination * 100)}>
                    {denomination}
                  </button>
                ))}
              </div>
            </div>
          )}

          {payMode === 'card' && <p className="checkout-pay-note">{t('op.checkout.cardExact')}</p>}

          {payMode === 'deposit' && (
            <p className="checkout-pay-note">
              {depositRemainder > 0
                ? t('op.checkout.depositPlusCash', { cover: formatMinorUnits(depositCover, currencyCode), rest: formatMinorUnits(depositRemainder, currencyCode) })
                : t('op.checkout.depositFull', { amount: formatMinorUnits(grandTotalMinorUnits, currencyCode) })}
            </p>
          )}

          {payMode === 'split' && (
            <div className="checkout-payments">
              {splitDrafts.map((draft, index) => (
                <div className="checkout-payment-row" key={index}>
                  <select
                    aria-label={t('op.checkout.paymentMethod')}
                    value={draft.method}
                    disabled={disabled}
                    onChange={(event) => updateSplitDraft(index, { method: event.currentTarget.value as CheckoutMethod })}
                  >
                    {getAvailableCheckoutMethods(splitDrafts, index).map((method) => (
                      <option key={method} value={method}>{checkoutMethodLabel(method, t)}</option>
                    ))}
                  </select>
                  <span className="checkout-payment-icon">{checkoutMethodIcons[draft.method]}</span>
                  <input
                    type="text"
                    inputMode="decimal"
                    aria-label={draft.method === 'cash' ? t('op.checkout.cashTendered') : t('op.checkout.paymentAmount')}
                    placeholder="0.00"
                    value={draft.amountText}
                    disabled={disabled}
                    onChange={(event) => updateSplitDraft(index, { amountText: event.currentTarget.value })}
                  />
                  <button
                    type="button"
                    className="checkout-payment-remove"
                    aria-label={t('op.checkout.removePaymentRow')}
                    disabled={disabled || splitDrafts.length <= 1}
                    onClick={() => removeSplitDraft(index)}
                  >
                    <X size={14} />
                  </button>
                </div>
              ))}
              <div className="checkout-payment-controls">
                <button type="button" disabled={disabled || !canAddSplitMethod} onClick={addSplitDraft}><Plus size={14} />{t('op.checkout.addPaymentMethod')}</button>
              </div>
            </div>
          )}

          {validation.error
            ? <p className="checkout-error">{validation.error}</p>
            : validation.changeMinorUnits > 0
              ? <p className="checkout-change"><span>{t('op.checkout.change')}</span><strong>{formatMinorUnits(validation.changeMinorUnits, currencyCode)}</strong></p>
              : <p className="checkout-balanced">{t('op.checkout.balanced')}</p>}
        </div>
      )}

      {!isZeroBill && endWithoutPayment && (
        <button type="button" className="checkout-end-plain" onClick={endWithoutPayment.onEnd} disabled={disabled}>
          {endWithoutPayment.label}
        </button>
      )}

      <div className="critical-confirmation-actions">
        <button type="button" onClick={onCancel} disabled={disabled}>{t('common.cancel')}</button>
        <button
          type="button"
          className={confirmVariant}
          disabled={isZeroBill ? disabled : !canConfirm}
          onClick={() => (isZeroBill
            ? endWithoutPayment?.onEnd()
            : onConfirm(buildCheckoutPayments(drafts, currencyCode, grandTotalMinorUnits)))}
        >
          {isZeroBill ? t('op.checkout.finish') : t('op.checkout.confirmAmount', { amount: formatMinorUnits(grandTotalMinorUnits, currencyCode) })}
        </button>
      </div>
    </>
  );
}
