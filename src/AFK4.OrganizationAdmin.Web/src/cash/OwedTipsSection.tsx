import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OwedShiftTipsDto, ShiftTipsDto } from '@afk4/contracts';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { createIdempotencyKey, formatMoney } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { CriticalActionConfirmation, Money } from '../operatorPrimitives';
import type { OperatorAuthSession } from '../authClient';

interface OwedTipsClient {
  owed(branchId: string): Promise<OwedShiftTipsDto[]>;
  payOut(shiftId: string, request: { idempotencyKey: string }): Promise<ShiftTipsDto>;
}

/**
 * Чаевые прошлых смен, которые так и не выдали: смену закрыли, а администратор денег не получил.
 * Это долг клуба перед сотрудником — раньше он нигде не всплывал. Выдают его из кассы открытой
 * сейчас смены: деньги уходят из ящика сегодня, отметка ставится на смену, где их заработали.
 * Долгов нет — блока нет.
 */
export function OwedTipsSection({
  client,
  session,
  branchId,
  currencyCode,
  shiftNonce = 0,
  onShiftChanged = () => {}
}: {
  client: OwedTipsClient | null;
  session: OperatorAuthSession | null;
  branchId: string;
  currencyCode: string;
  shiftNonce?: number;
  onShiftChanged?: () => void;
}) {
  const { t, formatDate } = useI18n();
  const [owed, setOwed] = useState<OwedShiftTipsDto[]>([]);
  const [reload, setReload] = useState(0);
  const [pending, setPending] = useState<OwedShiftTipsDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (client === null) return undefined;
    let active = true;
    // Отказ списка долгов — не повод трогать остальную кассу: блок просто не появится.
    Promise.resolve()
      .then(() => client.owed(branchId))
      .then((result) => { if (active) setOwed(Array.isArray(result) ? result : []); })
      .catch(() => {});
    return () => { active = false; };
  }, [client, branchId, shiftNonce, reload]);

  if (client === null || owed.length === 0) return null;

  const canPayOut = hasPermission(session, permissionNames.manageShiftCash);
  const day = (row: OwedShiftTipsDto) => formatDate(row.closedAtUtc ?? row.openedAtUtc);
  const name = (row: OwedShiftTipsDto) => row.recipientName || t('op.cash.shift.operatorFallback');

  const payOut = async () => {
    if (pending === null) return;
    const row = pending;
    setPending(null);
    setBusy(true);
    setError(null);
    try {
      await client.payOut(row.shiftId, { idempotencyKey: createIdempotencyKey('tips-payout') });
      // Выдача — движение наличных этой смены: ожидаемая сумма в ящике должна обновиться.
      onShiftChanged();
    } catch (failure) {
      setError(projectOperatorError(failure, t).detail);
    } finally {
      setBusy(false);
      setReload((value) => value + 1);
    }
  };

  return (
    <section className="cash-shift-movement-ledger cash-shift-tips" aria-label={t('op.cash.tips.owedTitle')}>
      <header>
        <h2>{t('op.cash.tips.owedTitle')}</h2>
        <strong>
          <Money
            minorUnits={owed.reduce((sum, row) => sum + row.owed.minorUnits, 0)}
            currencyCode={owed[0].owed.currencyCode || currencyCode}
          />
        </strong>
      </header>
      <p className="cash-shift-tips-lead">{t('op.cash.tips.owedLead')}</p>
      <ul className="cash-shift-movements">
        {owed.map((row) => (
          <li key={row.shiftId} className="out">
            <span />
            <strong>{t('op.cash.tips.owedRow', { date: day(row), name: name(row) })}</strong>
            <em />
            <span>
              {canPayOut ? (
                <button type="button" className="ui-btn ui-btn--sm ui-btn--primary" disabled={busy} onClick={() => setPending(row)}>
                  {t('op.cash.tips.payOut')}
                </button>
              ) : null}
            </span>
            <b><Money minorUnits={row.owed.minorUnits} currencyCode={row.owed.currencyCode || currencyCode} /></b>
          </li>
        ))}
      </ul>
      {error && <p className="ui-inline-error" role="alert">{error}</p>}

      {pending && (
        <CriticalActionConfirmation
          tone="warning"
          title={t('op.cash.tips.owedPayOutTitle', {
            amount: formatMoney({ currencyCode: pending.owed.currencyCode || currencyCode, minorUnits: pending.owed.minorUnits }, currencyCode),
            name: name(pending)
          })}
          detail={t('op.cash.tips.owedPayOutDetail', { date: day(pending) })}
          impact={t('op.cash.tips.payOutImpact')}
          confirmLabel={t('op.cash.tips.payOut')}
          onCancel={() => setPending(null)}
          onConfirm={() => void payOut()}
        />
      )}
    </section>
  );
}
