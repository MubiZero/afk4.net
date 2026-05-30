import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useI18n } from '@/i18n/I18nProvider';
import { DateRangeControl } from '../reports/DateRangeControl';
import type { DateRange } from '../reports/reportsModel';

export interface AuditDraft { action: string; outcome: string; targetType: string; }

export function AuditFilters({ range, onRangeChange, onApply, onReset }: {
  range: DateRange;
  onRangeChange: (range: DateRange) => void;
  onApply: (draft: AuditDraft) => void;
  onReset: () => void;
}) {
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
    <div className="flex flex-col gap-3">
      <DateRangeControl value={range} onChange={onRangeChange} />
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1 text-xs text-muted-foreground">
          {t('journal.filter.action')}
          <Input aria-label={t('journal.filter.action')} value={action} onChange={e => setAction(e.target.value)} />
        </label>
        <label className="flex flex-col gap-1 text-xs text-muted-foreground">
          {t('journal.filter.targetType')}
          <Input aria-label={t('journal.filter.targetType')} value={targetType} onChange={e => setTargetType(e.target.value)} />
        </label>
        <div className="flex flex-col gap-1 text-xs text-muted-foreground">
          {t('journal.filter.outcome')}
          <Select value={outcome} onValueChange={setOutcome}>
            <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t('journal.outcome.all')}</SelectItem>
              <SelectItem value="Succeeded">{t('journal.outcome.succeeded')}</SelectItem>
              <SelectItem value="Denied">{t('journal.outcome.denied')}</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <Button onClick={() => onApply({ action, outcome, targetType })}>{t('journal.filter.apply')}</Button>
        <Button variant="outline" onClick={reset}>{t('journal.filter.reset')}</Button>
      </div>
    </div>
  );
}
