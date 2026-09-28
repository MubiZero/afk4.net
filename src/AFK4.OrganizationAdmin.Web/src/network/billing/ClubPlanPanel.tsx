import { useEffect, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { ClubPlanKindNames, type ClubPlanDto } from '@afk4/contracts';
import { Money } from '../../operatorPrimitives';
import { formatMinorUnits } from '../../currencyFormat';
import { projectOperatorError } from '../../apiErrors';
import { SkeletonTiles } from '../../LoadingSkeleton';
import { FreePlanDevices, type FreePlanDevicesClient } from './FreePlanDevices';

export interface ClubPlanClient extends FreePlanDevicesClient {
  getPlan(): Promise<ClubPlanDto>;
  startTrial(): Promise<ClubPlanDto>;
  switchToPerPc(): Promise<ClubPlanDto>;
  promisePayment(): Promise<ClubPlanDto>;
}

type Action = 'trial' | 'perPc' | 'promise';

/**
 * Тариф клуба словами (спека тарифов клуба): сколько ПК, сколько из них платных и во что выйдет
 * месяц. Цену прежней сетки экран не показывает — 2 900 были рублями без пересчёта. Действия —
 * у владельца; каждое ждёт ответа сервера: это обязательство платить.
 */
export function ClubPlanPanel({
  client,
  canManage,
  onChanged = () => {}
}: {
  client: ClubPlanClient;
  canManage: boolean;
  // Тариф сменился — статус и период подписки рядом должны перечитаться.
  onChanged?: () => void;
}) {
  const { t, formatDate } = useI18n();
  const [plan, setPlan] = useState<ClubPlanDto | null>(null);
  const [failed, setFailed] = useState(false);
  const [busy, setBusy] = useState<Action | null>(null);
  const [error, setError] = useState<string | null>(null);
  // Каждое действие здесь — обязательство платить или разовый шанс: сначала слова и цена, потом шаг.
  const [confirming, setConfirming] = useState<Action | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let active = true;
    setFailed(false);
    client.getPlan().then((result) => { if (active) setPlan(result); }).catch(() => { if (active) setFailed(true); });
    return () => { active = false; };
  }, [client, attempt]);

  if (failed) {
    return (
      <div className="ui-inline-error" role="alert">
        <p>{t('op.network.plan.loadFailed')}</p>
        <button type="button" className="ui-btn ui-btn--sm" onClick={() => setAttempt((count) => count + 1)}>{t('op.management.state.retry')}</button>
      </div>
    );
  }
  if (plan === null) return <SkeletonTiles count={2} className="network-billing-grid" tileClassName="network-stat" />;

  const run = async (action: Action) => {
    setConfirming(null);
    setBusy(action);
    setError(null);
    try {
      const next = action === 'trial' ? await client.startTrial()
        : action === 'perPc' ? await client.switchToPerPc()
        : await client.promisePayment();
      setPlan(next);
      onChanged();
    } catch (failure) {
      setError(projectOperatorError(failure, t).detail);
      // Отказ обычно значит, что тариф уже не тот, что на экране (счёт оплатили, льготу взяли с
      // другого устройства): кнопки должны совпасть с тем, что знает сервер.
      void client.getPlan().then(setPlan).catch(() => {});
    } finally {
      setBusy(null);
    }
  };

  const priceText = formatMinorUnits(plan.pricePerDevice.minorUnits, plan.pricePerDevice.currencyCode);
  const confirmText = (action: Action) => {
    if (action === 'trial') {
      return t('op.network.plan.confirm.trial', { days: plan.trialDays ?? 30, included: plan.includedDevices, price: priceText });
    }
    if (action === 'perPc') {
      return t('op.network.plan.confirm.perPc', { included: plan.includedDevices, price: priceText, devices: plan.devices });
    }
    return t('op.network.plan.confirm.promise', {
      days: plan.promisedPaymentDays ?? 7,
      amount: plan.overdue ? formatMinorUnits(plan.overdue.minorUnits, plan.overdue.currencyCode) : '—'
    });
  };

  const price = <Money minorUnits={plan.pricePerDevice.minorUnits} currencyCode={plan.pricePerDevice.currencyCode} />;
  // Сервер до этого поля его не присылал; тогда ближайшее, что он знает, — «включено» тарифа.
  const freeLimit = plan.freeDeviceLimit ?? plan.includedDevices;
  return (
    <div className="network-plan" data-kind={plan.kind}>
      <div className="network-plan-head">
        <strong>{t(KIND_TITLE[plan.kind] ?? 'op.network.plan.kind.legacy')}</strong>
        <p>{describe(plan, t, formatDate)}</p>
      </div>
      <dl className="network-billing-grid">
        <div className="network-stat">
          <dt>{t('op.network.plan.devices')}</dt>
          <dd>{plan.devices}</dd>
        </div>
        {plan.kind === ClubPlanKindNames.PerPc ? (
          <>
            <div className="network-stat">
              <dt>{t('op.network.plan.billable', { included: plan.includedDevices })}</dt>
              <dd>{plan.billableDevices}</dd>
            </div>
            <div className="network-stat">
              <dt>{t('op.network.plan.monthly')}</dt>
              <dd><Money minorUnits={plan.estimatedMonthly.minorUnits} currencyCode={plan.estimatedMonthly.currencyCode} /></dd>
            </div>
          </>
        ) : null}
        <div className="network-stat">
          <dt>{t('op.network.plan.pricePerDevice', { included: plan.includedDevices })}</dt>
          <dd>{price}</dd>
        </div>
        {plan.overdue ? (
          <div className="network-stat network-stat--attention">
            <dt>{t('op.network.plan.overdue')}</dt>
            <dd><Money minorUnits={plan.overdue.minorUnits} currencyCode={plan.overdue.currencyCode} /></dd>
          </div>
        ) : null}
      </dl>
      {plan.promisedPaymentUntilUtc ? (
        <p className="network-plan-note">{t('op.network.plan.promisedUntil', { date: formatDate(plan.promisedPaymentUntilUtc) })}</p>
      ) : null}
      {plan.fallbackAtUtc ? (
        <p className="network-plan-note network-plan-note--attention">
          {/* Уходят на бесплатный — и предел его, а не «сколько ПК без платы» у тарифа за ПК. */}
          {plan.devices > freeLimit
            ? t('op.network.plan.fallbackLimited', { date: formatDate(plan.fallbackAtUtc), included: freeLimit, devices: plan.devices })
            : t('op.network.plan.fallback', { date: formatDate(plan.fallbackAtUtc) })}
        </p>
      ) : null}
      {canManage ? (
        <div className="network-plan-actions">
          {plan.trialAvailable ? (
            <button type="button" className="ui-btn ui-btn--primary" disabled={busy !== null} onClick={() => setConfirming('trial')}>
              {t('op.network.plan.startTrial', { days: plan.trialDays ?? 30 })}
            </button>
          ) : null}
          {plan.canSwitchToPerPc ? (
            <button type="button" className="ui-btn" disabled={busy !== null} onClick={() => setConfirming('perPc')}>
              {t('op.network.plan.switchToPerPc')}
            </button>
          ) : null}
          {plan.promisedPaymentAvailable ? (
            <button type="button" className="ui-btn" disabled={busy !== null} onClick={() => setConfirming('promise')}>
              {t('op.network.plan.promisePayment', { days: plan.promisedPaymentDays ?? 7 })}
            </button>
          ) : null}
        </div>
      ) : null}
      {confirming !== null && canManage ? (
        <div className="network-plan-confirm" role="group" aria-label={t(ACTION_LABEL[confirming], { days: confirming === 'trial' ? plan.trialDays ?? 30 : plan.promisedPaymentDays ?? 7 })}>
          <p>{confirmText(confirming)}</p>
          <div className="network-plan-actions">
            <button type="button" className="ui-btn ui-btn--primary" disabled={busy !== null} onClick={() => void run(confirming)}>
              {t('op.network.plan.confirm.yes')}
            </button>
            <button type="button" className="ui-btn" disabled={busy !== null} onClick={() => setConfirming(null)}>
              {t('op.network.plan.confirm.no')}
            </button>
          </div>
        </div>
      ) : null}
      {error && <p className="ui-inline-error" role="alert">{error}</p>}
      {(plan.devicesOutsidePlan ?? 0) > 0 ? (
        <FreePlanDevices client={client} canManage={canManage} onChanged={() => void client.getPlan().then(setPlan).catch(() => {})} />
      ) : null}
      {plan.referralCode ? <ReferralBlock plan={plan} /> : null}
      <FreePlanTerms freeDevices={freeLimit} />
    </div>
  );
}

/**
 * Условия бесплатного тарифа — коротко и простыми словами (владелец, 2026-09-26): что клуб получает,
 * что показывается на его ПК и что клуб может сделать, если реклама не подходит.
 */
function FreePlanTerms({ freeDevices }: { freeDevices: number }) {
  const { t } = useI18n();
  const points: MessageKey[] = [
    'op.network.plan.terms.pcs',
    'op.network.plan.terms.ads',
    'op.network.plan.terms.checked',
    'op.network.plan.terms.distributor',
    'op.network.plan.terms.money',
    'op.network.plan.terms.unpaid'
  ];
  return (
    <details className="network-plan-terms">
      <summary>{t('op.network.plan.terms.title')}</summary>
      <ol>
        {points.map((key) => <li key={key}>{t(key, { count: freeDevices })}</li>)}
      </ol>
    </details>
  );
}

/**
 * «Приведи клуб»: код клуба и что за него будет. Код диктуют голосом — поэтому он крупно и без
 * похожих знаков; кнопка копирует его целиком.
 */
function ReferralBlock({ plan }: { plan: ClubPlanDto }) {
  const { t } = useI18n();
  const [copied, setCopied] = useState(false);
  const copy = () => {
    navigator.clipboard?.writeText(plan.referralCode ?? '').then(() => setCopied(true)).catch(() => {});
  };

  return (
    <div className="network-plan-referral">
      <strong>{t('op.network.referral.title')}</strong>
      <p>{t('op.network.referral.lead')}</p>
      <div className="network-plan-referral-code">
        <code>{plan.referralCode}</code>
        <button type="button" className="ui-btn ui-btn--sm" onClick={copy}>{copied ? t('op.network.referral.copied') : t('op.network.referral.copy')}</button>
      </div>
      {(plan.referredClubs ?? 0) > 0 || (plan.freeMonths ?? 0) > 0 ? (
        <p className="network-plan-note">{t('op.network.referral.earned', { clubs: plan.referredClubs ?? 0, months: plan.freeMonths ?? 0 })}</p>
      ) : null}
    </div>
  );
}

const ACTION_LABEL: Record<Action, MessageKey> = {
  trial: 'op.network.plan.startTrial',
  perPc: 'op.network.plan.switchToPerPc',
  promise: 'op.network.plan.promisePayment'
};

const KIND_TITLE = {
  [ClubPlanKindNames.Free]: 'op.network.plan.kind.free',
  [ClubPlanKindNames.PerPc]: 'op.network.plan.kind.perPc',
  [ClubPlanKindNames.Trial]: 'op.network.plan.kind.trial',
  [ClubPlanKindNames.Legacy]: 'op.network.plan.kind.legacy'
} as const;

function describe(
  plan: ClubPlanDto,
  t: ReturnType<typeof useI18n>['t'],
  formatDate: ReturnType<typeof useI18n>['formatDate']
): string {
  switch (plan.kind) {
    case ClubPlanKindNames.Free:
      return t('op.network.plan.free.lead', { included: plan.includedDevices });
    case ClubPlanKindNames.Trial:
      return t('op.network.plan.trial.lead', { date: plan.trialEndsAtUtc ? formatDate(plan.trialEndsAtUtc) : '—', included: plan.includedDevices });
    case ClubPlanKindNames.PerPc:
      return t('op.network.plan.perPc.lead', { included: plan.includedDevices });
    default:
      return t('op.network.plan.legacy.lead');
  }
}
