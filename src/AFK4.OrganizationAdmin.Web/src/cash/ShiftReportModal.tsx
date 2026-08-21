import { useI18n } from '@afk4/i18n';
import { Printer } from 'lucide-react';
import { PanelModal } from '../PanelModal';
import { formatTime } from '../operatorHelpers';
import { Money } from '../operatorPrimitives';
import type { ShiftReportData } from './shiftReport';

// Презентационная форма отчёта по смене: X (промежуточный, смена открыта) или Z (итог закрытия).
// Read-only; печать — снаружи через onPrint (cash/shiftReport.printShiftReport).
export function ShiftReportModal({
  variant,
  data,
  currencyCode,
  onClose,
  onPrint
}: {
  variant: 'x' | 'z';
  data: ShiftReportData;
  currencyCode: string;
  onClose: () => void;
  onPrint: () => void;
}) {
  const { t } = useI18n();
  const title = variant === 'x' ? t('op.cash.report.xTitle') : t('op.cash.report.zTitle');
  const subtitle = variant === 'x' ? t('op.cash.report.xSubtitle') : t('op.cash.report.zSubtitle');
  const money = (value: { currencyCode: string; minorUnits: number } | null) =>
    value === null ? t('op.cash.shift.notClosed') : <Money minorUnits={value.minorUnits} currencyCode={currencyCode} />;

  return (
    <PanelModal title={title} subtitle={subtitle} onClose={onClose}>
      <div className="cash-report">
        <p className="cash-report-time">
          {t('op.cash.report.opened')}: {formatTime(data.openedAtUtc)}
          {data.closedAtUtc ? ` · ${t('op.cash.report.closed')}: ${formatTime(data.closedAtUtc)}` : ''}
        </p>

        <section className="cash-report-section">
          <h3>{t('op.cash.report.revenueSection')}</h3>
          <div className="cash-shift-row"><span>{t('op.shifts.earned')}</span><strong>{money(data.earned.total)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.shifts.time')}</span><strong>{money(data.earned.time)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.shifts.goods')}</span><strong>{money(data.earned.goods)}</strong></div>
          {/* Удержанная за неявку предоплата входит в итог выручки, поэтому в документе, который
              сверяют, строка стоит всегда — даже нулевая. Иначе итог не раскладывается. */}
          <div className="cash-shift-row"><span>{t('op.shifts.noShow')}</span><strong>{money(data.earned.noShow)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.shifts.cash')}</span><strong>{money(data.inflow.cash)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.shifts.nonCash')}</span><strong>{money(data.inflow.nonCash)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.shifts.walletTopUps')}</span><strong>{money(data.inflow.walletTopUps)}</strong></div>
        </section>

        <section className="cash-report-section">
          <h3>{t('op.cash.report.reconcileSection')}</h3>
          <div className="cash-shift-row"><span>{t('op.cash.shift.starting')}</span><strong>{money(data.cash.starting)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.cash.shift.expected')}</span><strong>{money(data.cash.expected)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.cash.shift.counted')}</span><strong>{money(data.cash.counted)}</strong></div>
          <div className={`cash-shift-row${data.cash.difference !== null && data.cash.difference.minorUnits !== 0 ? ' attention' : ''}`}>
            <span>{t('op.cash.shift.difference')}</span><strong>{money(data.cash.difference)}</strong>
          </div>
        </section>

        <button type="button" className="ui-btn ui-btn--primary ui-btn--lg ui-btn--block cash-primary-action" onClick={onPrint}>
          <Printer size={15} aria-hidden="true" />
          {t('op.cash.report.print')}
        </button>
      </div>
    </PanelModal>
  );
}
