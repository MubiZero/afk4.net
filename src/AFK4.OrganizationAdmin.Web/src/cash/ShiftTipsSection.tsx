import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { ShiftTipsDto } from '@afk4/contracts';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { createIdempotencyKey, formatMoney, formatTime } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { CriticalActionConfirmation, Money } from '../operatorPrimitives';
import type { OperatorAuthSession } from '../authClient';

interface TipsClient {
  forShift(shiftId: string): Promise<ShiftTipsDto>;
  reverse(shiftId: string, ledgerEntryId: string): Promise<ShiftTipsDto>;
  payOut(shiftId: string, request: { idempotencyKey: string }): Promise<ShiftTipsDto>;
}

// Ответ не той формы (старый сервер без чаевых, прокси с заглушкой) — блока нет, а не упавший экран кассы.
function shaped(result: ShiftTipsDto | null | undefined): ShiftTipsDto | null {
  return result && Array.isArray(result.tips) && result.total ? result : null;
}

type Pending = { kind: 'reverse'; ledgerEntryId: string; amount: number } | { kind: 'payout'; amount: number };

/**
 * Чаевые за смену: сколько пришло с экранов ПК, кому, что уже выдано. Пока чаевых нет, блока нет
 * вовсе — у клуба, который их не включал, это вечный ноль. Имени игрока здесь нет: сумма и ПК.
 */
export function ShiftTipsSection({
  client,
  session,
  shiftId,
  currencyCode,
  shiftNonce = 0,
  onShiftChanged = () => {}
}: {
  // Клиента передаёт экран кассы — тот же, из которого он берёт смену; нет клиента — нет блока.
  client: TipsClient | null;
  session: OperatorAuthSession | null;
  shiftId: string;
  currencyCode: string;
  shiftNonce?: number;
  onShiftChanged?: () => void;
}) {
  const { t } = useI18n();
  const [tips, setTips] = useState<ShiftTipsDto | null>(null);
  const [pending, setPending] = useState<Pending | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (client === null) return undefined;
    let active = true;
    // Отказ списка чаевых — не повод трогать остальную смену: блок просто не появится.
    Promise.resolve()
      .then(() => client.forShift(shiftId))
      .then((result) => { if (active) setTips(shaped(result)); })
      .catch(() => {});
    return () => { active = false; };
  }, [client, shiftId, shiftNonce]);

  if (client === null || tips === null || tips.tips.length === 0) return null;

  const canReverse = hasPermission(session, permissionNames.manageTips);
  const canPayOut = hasPermission(session, permissionNames.manageShiftCash);
  const paidOut = tips.paidOut?.minorUnits ?? 0;
  const unpaid = Math.max(0, tips.total.minorUnits - paidOut);
  const money = (minorUnits: number) => formatMoney({ currencyCode: tips.total.currencyCode || currencyCode, minorUnits }, currencyCode);

  const run = async () => {
    if (pending === null) return;
    const action = pending;
    setPending(null);
    setBusy(true);
    setError(null);
    try {
      if (action.kind === 'reverse') {
        setTips(shaped(await client.reverse(shiftId, action.ledgerEntryId)));
      } else {
        setTips(shaped(await client.payOut(shiftId, { idempotencyKey: createIdempotencyKey('tips-payout') })));
        // Выдача — движение наличных: ожидаемая сумма в ящике и список движений должны обновиться.
        onShiftChanged();
      }
    } catch (failure) {
      setError(projectOperatorError(failure, t).detail);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="cash-shift-movement-ledger cash-shift-tips" aria-label={t('op.cash.tips.title')}>
      <header>
        <h2>{t('op.cash.tips.title')}</h2>
        <strong><Money minorUnits={tips.total.minorUnits} currencyCode={tips.total.currencyCode || currencyCode} /></strong>
      </header>
      <p className="cash-shift-tips-lead">
        {t('op.cash.tips.lead', { name: tips.recipientName || t('op.cash.shift.operatorFallback') })}
      </p>
      <ul className="cash-shift-movements">
        {tips.tips.map((tip) => (
          <li key={tip.ledgerEntryId} className={tip.reversed ? 'reversed' : 'in'}>
            <span>{formatTime(tip.createdAtUtc)}</span>
            <strong>{tip.seatLabel ?? '—'}</strong>
            <em>{tip.reversed ? t('op.cash.tips.reversed') : ''}</em>
            <span>
              {canReverse && !tip.reversed ? (
                <button
                  type="button"
                  className="ui-btn ui-btn--sm"
                  disabled={busy}
                  onClick={() => setPending({ kind: 'reverse', ledgerEntryId: tip.ledgerEntryId, amount: tip.amount.minorUnits })}
                >
                  {t('op.cash.tips.reverse')}
                </button>
              ) : null}
            </span>
            <b><Money minorUnits={tip.amount.minorUnits} currencyCode={tip.amount.currencyCode || currencyCode} /></b>
          </li>
        ))}
      </ul>
      <footer>
        <span>{paidOut > 0 ? t('op.cash.tips.paidOut', { amount: money(paidOut) }) : t('op.cash.tips.notPaidOut')}</span>
        {canPayOut && unpaid > 0 ? (
          <button type="button" className="ui-btn ui-btn--sm ui-btn--primary" disabled={busy} onClick={() => setPending({ kind: 'payout', amount: unpaid })}>
            {t('op.cash.tips.payOut')} <Money minorUnits={unpaid} currencyCode={currencyCode} />
          </button>
        ) : null}
      </footer>
      {error && <p className="ui-inline-error" role="alert">{error}</p>}

      {pending?.kind === 'reverse' && (
        <CriticalActionConfirmation
          title={t('op.cash.tips.reverseTitle', { amount: money(pending.amount) })}
          detail={t('op.cash.tips.reverseDetail')}
          impact={t('op.cash.tips.reverseImpact')}
          confirmLabel={t('op.cash.tips.reverse')}
          onCancel={() => setPending(null)}
          onConfirm={() => void run()}
        />
      )}
      {pending?.kind === 'payout' && (
        <CriticalActionConfirmation
          tone="warning"
          title={t('op.cash.tips.payOutTitle', { amount: money(pending.amount), name: tips.recipientName || t('op.cash.shift.operatorFallback') })}
          detail={t('op.cash.tips.payOutDetail')}
          impact={t('op.cash.tips.payOutImpact')}
          confirmLabel={t('op.cash.tips.payOut')}
          onCancel={() => setPending(null)}
          onConfirm={() => void run()}
        />
      )}
    </section>
  );
}
