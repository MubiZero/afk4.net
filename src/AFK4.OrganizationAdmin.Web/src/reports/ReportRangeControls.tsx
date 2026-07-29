import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import type { ReportDateRange } from './reportRange';

export function ReportRangeControls({ range, onChange, onRefresh, onExport, exporting = false }: {
  range: ReportDateRange;
  onChange: (range: ReportDateRange) => void;
  onRefresh: () => void;
  onExport?: () => void;
  exporting?: boolean;
}): JSX.Element {
  const { t } = useI18n();
  return (
    <div className="reports-range" aria-label={t('op.reports.range')}>
      <label>{t('op.common.from')}<input type="date" value={range.from} onChange={(event) => onChange({ ...range, from: event.target.value })} /></label>
      <label>{t('op.common.to')}<input type="date" value={range.to} onChange={(event) => onChange({ ...range, to: event.target.value })} /></label>
      <button type="button" className="ui-btn ui-btn--primary" onClick={onRefresh}>{t('op.common.apply')}</button>
      {onExport ? <button type="button" className="ui-btn" disabled={exporting} onClick={onExport}>{t('op.reports.export')}</button> : null}
    </div>
  );
}
