import { useEffect, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { ClubPlanKindNames, type ClubPlanDto } from '@afk4/contracts';
import { Money } from '../../operatorPrimitives';
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

  useEffect(() => {
    let active = true;
    client.getPlan().then((result) => { if (active) setPlan(result); }).catch(() => { if (active) setFailed(true); });
    return () => { active = false; };
  }, [client]);

  if (failed) return <p className="ui-inline-error" role="alert">{t('op.network.plan.loadFailed')}</p>;
  if (plan === null) return <SkeletonTiles count={2} className="network-billing-grid" tileClassName="network-stat" />;

  const run = async (action: Action) => {
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
    } finally {
      setBusy(null);
    }
  };

  const price = <Money minorUnits={plan.pricePerDevice.minorUnits} currencyCode={plan.pricePerDevice.currencyCode} />;
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
          {plan.devices > plan.includedDevices
            ? t('op.network.plan.fallbackLimited', { date: formatDate(plan.fallbackAtUtc), included: plan.includedDevices, devices: plan.devices })
            : t('op.network.plan.fallback', { date: formatDate(plan.fallbackAtUtc) })}
        </p>
      ) : null}
      {canManage ? (
        <div className="network-plan-actions">
          {plan.trialAvailable ? (
            <button type="button" className="ui-btn ui-btn--primary" disabled={busy !== null} onClick={() => void run('trial')}>
              {t('op.network.plan.startTrial', { days: plan.trialDays ?? 30 })}
            </button>
          ) : null}
          {plan.canSwitchToPerPc ? (
            <button type="button" className="ui-btn" disabled={busy !== null} onClick={() => void run('perPc')}>
              {t('op.network.plan.switchToPerPc')}
            </button>
          ) : null}
          {plan.promisedPaymentAvailable ? (
            <button type="button" className="ui-btn" disabled={busy !== null} onClick={() => void run('promise')}>
              {t('op.network.plan.promisePayment', { days: plan.promisedPaymentDays ?? 7 })}
            </button>
          ) : null}
        </div>
      ) : null}
      {error && <p className="ui-inline-error" role="alert">{error}</p>}
      {(plan.devicesOutsidePlan ?? 0) > 0 ? (
        <FreePlanDevices client={client} canManage={canManage} onChanged={() => void client.getPlan().then(setPlan).catch(() => {})} />
      ) : null}
      {plan.referralCode ? <ReferralBlock plan={plan} /> : null}
      <FreePlanTerms />
    </div>
  );
}

/**
 * Условия бесплатного тарифа — коротко и простыми словами (владелец, 2026-09-26): что клуб получает,
 * что показывается на его ПК и что клуб может сделать, если реклама не подходит.
 */
function FreePlanTerms() {
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
        {points.map((key) => <li key={key}>{t(key)}</li>)}
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
