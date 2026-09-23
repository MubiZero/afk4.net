import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import type { ReportDateRange } from './reportRange';
import { SkeletonControl, SkeletonLine, SkeletonTiles } from '../LoadingSkeleton';

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

// Заглушка панели периода: две даты и кнопки — чтобы при первой загрузке отчёта панель стояла на
// своём месте, а не вдвигалась сверху вместе с цифрами.
export function ReportRangeSkeleton({ exportable }: { exportable: boolean }): JSX.Element {
  return (
    <div className="reports-range" aria-hidden="true">
      <label><SkeletonLine width="3em" /><SkeletonControl width="9.5rem" /></label>
      <label><SkeletonLine width="3em" /><SkeletonControl width="9.5rem" /></label>
      <SkeletonControl width="7rem" />
      {exportable ? <SkeletonControl width="7rem" /> : null}
    </div>
  );
}

// Строка цифр отчёта (.reports-figures) — по плитке на цифру.
export function ReportFiguresSkeleton({ count }: { count: number }): JSX.Element {
  return <SkeletonTiles count={count} className="reports-figures" />;
}
