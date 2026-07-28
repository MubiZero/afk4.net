import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { EmptyState } from '../../operatorPrimitives';
import type { ReportView } from './reportModel';

export function ReportTable({ view, onExport }: { view: ReportView; onExport: () => void }): JSX.Element {
  const { t } = useI18n();
  const grid = view.columns.map(() => '1fr').join(' ');

  return (
    <div className="reports-history-body">
      <div className="reports-history-toolbar">
        <button type="button" className="ui-btn" onClick={onExport}>{t('op.reports.export')}</button>
      </div>

      {view.summaryCards.length > 0 && (
        <div className="reports-summary-grid">
          {view.summaryCards.map((card) => (
            <div key={card.labelKey} className="reports-summary-card">
              <span className="reports-summary-label">{t(card.labelKey)}</span>
              <strong className="reports-summary-value">{card.value}</strong>
            </div>
          ))}
        </div>
      )}

      {view.rows.length === 0 ? (
        <EmptyState title={t('op.reports.empty')} />
      ) : (
        <div className="table-panel">
          <div className="ctable-head" style={{ gridTemplateColumns: grid }} aria-hidden="true">
            {view.columns.map((col) => <span key={col.key}>{t(col.labelKey)}</span>)}
          </div>
          <div className="ctable-body">
            {view.rows.map((row, index) => (
              <div key={index} className="ctable-row" style={{ gridTemplateColumns: grid }}>
                {view.columns.map((col) => <span key={col.key}>{row[col.key]}</span>)}
              </div>
            ))}
          </div>
        </div>
      )}

      <p className="reports-history-limit-note">{t('op.reports.limitNote')}</p>
    </div>
  );
}
