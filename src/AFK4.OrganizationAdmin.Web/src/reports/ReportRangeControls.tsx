import type { JSX, ReactNode } from 'react';
import { Loader2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { ReportDateRange } from './reportRange';
import { DeferredSkeleton, SkeletonTiles } from '../LoadingSkeleton';
import { useDeferredFlag } from '../useDeferredFlag';
import { projectOperatorError, type OperatorErrorProjection } from '../apiErrors';
import { LoadFailureState } from '../operatorPrimitives';

// Тело вкладки отчёта: панель периода, затем заглушка, отказ или содержимое. Своего заголовка нет:
// имя отчёта — выбранная вкладка в шапке раздела «Отчёты». Раньше каждый отчёт жил во вложенном
// экране Управления, и под вкладкой «Сводка» стояло ещё раз «Главное за выбранный день / Сводка».
export function ReportScreen({ state, skeleton, failure, onRetry, controls, narrow = false, children }: {
  state: 'loading' | 'error' | 'ready';
  skeleton: ReactNode;
  failure?: OperatorErrorProjection;
  onRetry?: () => void;
  controls?: ReactNode;
  /** Форма, а не таблица, — мера чтения вместо всей ширины. */
  narrow?: boolean;
  children: ReactNode;
}): JSX.Element {
  const { t } = useI18n();
  return (
    <div className={`reports-screen${narrow ? ' reports-screen--narrow' : ''}`}>
      {controls}
      {state === 'loading' ? (
        <DeferredSkeleton>{skeleton}</DeferredSkeleton>
      ) : state === 'error' ? (
        <LoadFailureState title={t('op.management.state.errorTitle')} failure={failure ?? projectOperatorError(undefined, t)} onRetry={onRetry} />
      ) : children}
    </div>
  );
}

// Панель периода от ответа не зависит и стоит в ReportScreen.controls — на месте и при
// загрузке, и при отказе. `refreshing` — идёт запрос за новый период поверх показанных данных.
export function ReportRangeControls({ range, onChange, onRefresh, onExport, exporting = false, refreshing = false }: {
  range: ReportDateRange;
  onChange: (range: ReportDateRange) => void;
  onRefresh: () => void;
  onExport?: () => void;
  exporting?: boolean;
  refreshing?: boolean;
}): JSX.Element {
  const { t } = useI18n();
  // Тот же порог, что у заглушки: быстрый ответ не мигает признаком обновления.
  const showRefreshing = useDeferredFlag(refreshing);
  return (
    <div className="reports-range" aria-label={t('op.reports.range')}>
      <label>{t('op.common.from')}<input type="date" value={range.from} onChange={(event) => onChange({ ...range, from: event.target.value })} /></label>
      <label>{t('op.common.to')}<input type="date" value={range.to} onChange={(event) => onChange({ ...range, to: event.target.value })} /></label>
      <button type="button" className="ui-btn ui-btn--primary" onClick={onRefresh}>{t('op.common.apply')}</button>
      {onExport ? <button type="button" className="ui-btn" disabled={exporting} onClick={onExport}>{t('op.reports.export')}</button> : null}
      {showRefreshing ? (
        <span className="reports-refreshing" role="status">
          <Loader2 size={14} className="ui-spinner" aria-hidden="true" />
          {t('op.reports.refreshing')}
        </span>
      ) : null}
    </div>
  );
}

// Тело отчёта. Пока идёт запрос за новый период, прошлые цифры остаются на месте, но приглушены:
// они уже не за те даты, что в полях, и выдавать их за ответ нельзя.
export function ReportBody({ refreshing, children }: { refreshing: boolean; children: ReactNode }): JSX.Element {
  const dimmed = useDeferredFlag(refreshing);
  return <div className="reports-body" aria-busy={dimmed || undefined} data-refreshing={dimmed || undefined}>{children}</div>;
}

// Строка цифр отчёта (.reports-figures) — по плитке на цифру.
export function ReportFiguresSkeleton({ count }: { count: number }): JSX.Element {
  return <SkeletonTiles count={count} className="reports-figures" />;
}
