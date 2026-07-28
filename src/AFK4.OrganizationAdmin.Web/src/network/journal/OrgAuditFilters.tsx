import { useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { presetRange, isoToDateInput, dateInputToFromUtc, dateInputToToUtc, type DateRange, type RangePreset } from './dateRange';

export interface AuditDraft {
  action: string;
  outcome: string;
  targetType: string;
}

const PRESETS: { preset: RangePreset; labelKey: 'op.network.journal.range.today' | 'op.network.journal.range.7d' | 'op.network.journal.range.30d' }[] = [
  { preset: 'today', labelKey: 'op.network.journal.range.today' },
  { preset: '7d', labelKey: 'op.network.journal.range.7d' },
  { preset: '30d', labelKey: 'op.network.journal.range.30d' }
];

export function OrgAuditFilters({ range, onRangeChange, onApply, onReset }: {
  range: DateRange;
  onRangeChange: (range: DateRange) => void;
  onApply: (draft: AuditDraft) => void;
  onReset: () => void;
}): JSX.Element {
  const { t } = useI18n();
  const [action, setAction] = useState('');
  const [outcome, setOutcome] = useState('all');
  const [targetType, setTargetType] = useState('');

  function reset() {
    setAction('');
    setOutcome('all');
    setTargetType('');
    onReset();
  }

  return (
    <div className="network-journal-filters mgmt-form">
      <div className="network-journal-presets">
        {PRESETS.map((p) => (
          <button key={p.preset} type="button" className="ui-btn" onClick={() => onRangeChange(presetRange(p.preset, new Date()))}>
            {t(p.labelKey)}
          </button>
        ))}
      </div>
      <div className="mgmt-form-grid">
        <label>
          {t('op.network.journal.range.from')}
          <input
            type="date"
            value={isoToDateInput(range.fromUtc)}
            onChange={(e) => onRangeChange({ fromUtc: dateInputToFromUtc(e.currentTarget.value), toUtc: range.toUtc })}
          />
        </label>
        <label>
          {t('op.network.journal.range.to')}
          <input
            type="date"
            value={isoToDateInput(range.toUtc)}
            onChange={(e) => onRangeChange({ fromUtc: range.fromUtc, toUtc: dateInputToToUtc(e.currentTarget.value) })}
          />
        </label>
        <label>
          {t('op.network.journal.filter.action')}
          <input value={action} onChange={(e) => setAction(e.currentTarget.value)} />
        </label>
        <label>
          {t('op.network.journal.filter.targetType')}
          <input value={targetType} onChange={(e) => setTargetType(e.currentTarget.value)} />
        </label>
        <label>
          {t('op.network.journal.filter.outcome')}
          <select value={outcome} onChange={(e) => setOutcome(e.currentTarget.value)}>
            <option value="all">{t('op.network.journal.outcome.all')}</option>
            <option value="Succeeded">{t('op.network.journal.outcome.succeeded')}</option>
            <option value="Denied">{t('op.network.journal.outcome.denied')}</option>
          </select>
        </label>
      </div>
      <div className="mgmt-form-actions">
        <button type="button" className="ui-btn ui-btn--primary" onClick={() => onApply({ action, outcome, targetType })}>
          {t('op.network.journal.filter.apply')}
        </button>
        <button type="button" className="ui-btn" onClick={reset}>
          {t('op.network.journal.filter.reset')}
        </button>
      </div>
    </div>
  );
}
