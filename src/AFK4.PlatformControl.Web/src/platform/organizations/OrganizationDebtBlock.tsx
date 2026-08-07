import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { DebtRow } from '@/api/types';
import { dunningStageLabelKey } from '@/platform/billing/debtModel';

// Паспорт уже показывал чип «Просрочен платёж», но не отвечал, сколько именно и сколько дней —
// человек уходил на экран «Деньги» за цифрой. `row` — это строка из того же `debt.listDebt()`,
// что кормит раздел «Задолженность»; `null`, если организации в очереди должников нет.
export function OrganizationDebtBlock({ row }: { row: DebtRow | null }) {
  const { t, formatCurrency, formatDate } = useI18n();
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
          {t('platform.organization.passport.debt.invoice', { number: row.oldestOverdueInvoiceNumber })}
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
