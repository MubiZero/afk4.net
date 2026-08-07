import { useI18n } from '@afk4/i18n';
import type { OrganizationBillingStatusDto } from '../operatorApiClients';
import { formatMinorUnits } from '../currencyFormat';

// Non-dismissible strip in the same shell slot as SupportModeBanner (App.tsx wires the two as
// mutually exclusive — a support session already puts the same arrears in front of the platform
// staffer running the club's screen, so stacking both would double the banner height for nothing).
// The club learning about an overdue invoice only from an email that's easy to miss is the whole
// point of this banner: say it here, before the platform decides to suspend the club.
//
// Tone is deliberately NOT uniform: an active payment grace is a calm, dated state — the platform
// already agreed to wait — while arrears with no grace is the urgent one. Renders nothing when
// there's no debt at all, so the caller can pass the loaded status straight through without its own
// `inArrears` check.
export function BillingStatusBanner({ status }: { status: OrganizationBillingStatusDto }) {
  const { t, formatDate } = useI18n();

  if (!status.inArrears) {
    return null;
  }

  const inGrace = status.graceUntilUtc !== null;

  return (
    <div
      className={inGrace ? 'billing-status-banner billing-status-banner-grace' : 'billing-status-banner billing-status-banner-overdue'}
      role="status"
    >
      <span className="billing-status-banner-badge">
        {t(inGrace ? 'op.billing.banner.graceTitle' : 'op.billing.banner.title')}
      </span>
      <span className="billing-status-banner-message">
        {inGrace
          ? t('op.billing.banner.grace', { date: formatDate(status.graceUntilUtc!) })
          : t('op.billing.banner.overdue', {
              // String, not number: a numeric arg gets locale digit-grouping from
              // IntlMessageFormat, so invoice #1234 would render "Счёт №1 234".
              number: status.oldestOverdueInvoiceNumber !== null ? String(status.oldestOverdueInvoiceNumber) : '',
              days: status.daysOverdue,
              amount: formatMinorUnits(status.outstandingMinorUnits, status.currencyCode)
            })}
      </span>
    </div>
  );
}
