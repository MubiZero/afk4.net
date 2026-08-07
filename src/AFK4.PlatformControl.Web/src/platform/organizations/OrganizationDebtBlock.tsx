import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { DebtRow } from '@/api/types';
import { dunningStageLabelKey } from '@/platform/billing/debtModel';

// Паспорт уже показывал чип «Просрочен платёж», но не отвечал, сколько именно и сколько дней —
// человек уходил на экран «Деньги» за цифрой. `row` — это строка из того же `debt.listDebt()`,
// что кормит раздел «Задолженность»; `null`, если организации в очереди должников нет.
//
// `status` различает «мы спросили и долгов нет» от «мы не спрашивали» (нет права
// platform.billing.view или запрос упал): сотрудник поддержки без доступа к биллингу не должен
// увидеть уверенное «Долгов нет» — это выглядит как утверждение о факте, которого мы не проверяли.
export function OrganizationDebtBlock({ row, status = 'ready' }: { row: DebtRow | null; status?: 'unknown' | 'ready' }) {
  const { t, formatCurrency, formatDate } = useI18n();

  if (status === 'unknown') {
    return (
      <div className="pc-passport-debt" data-testid="passport-debt">
        <Badge variant="outline">{t('platform.organization.passport.debt.unknown')}</Badge>
      </div>
    );
  }

  const owesSomething = row !== null && row.outstandingMinorUnits > 0;

  if (!owesSomething) {
    return (
      <div className="pc-passport-debt" data-testid="passport-debt">
        <Badge variant="outline">
          {row?.settledButSuspended === true ? t('platform.debt.settledButSuspended') : t('platform.organization.passport.debt.none')}
        </Badge>
      </div>
    );
  }

  return (
    <div className="pc-passport-debt" data-testid="passport-debt">
      <div className="pc-passport-debt-top">
        <span className="pc-passport-debt-amount ui-money">
          {formatCurrency(minorToMajor(row.outstandingMinorUnits), row.currencyCode)}
        </span>
        <Badge data-testid="passport-debt-stage" variant={row.graceUntilUtc !== null ? 'secondary' : 'destructive'}>
          {t(dunningStageLabelKey(row.dunningStage))}
        </Badge>
      </div>
      {row.oldestOverdueInvoiceNumber !== null ? (
        <span className="pc-passport-debt-invoice">
          {/* String, not number: IntlMessageFormat applies locale digit-grouping to a numeric
              argument, so invoice #1234 would render "Счёт №1 234". */}
          {t('platform.organization.passport.debt.invoice', { number: String(row.oldestOverdueInvoiceNumber) })}
        </span>
      ) : null}
      <span className="pc-passport-debt-note">
        {row.graceUntilUtc !== null
          ? t('platform.debt.grace.until', { date: formatDate(row.graceUntilUtc) })
          : t('platform.debt.row.daysOverdue', { days: row.daysOverdue })}
      </span>
    </div>
  );
}
