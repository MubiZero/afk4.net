import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useI18n } from '@/i18n/I18nProvider';
import {
  isoToDateInput, dateInputToFromUtc, dateInputToToUtc, presetRange,
  type DateRange, type RangePreset
} from './reportsModel';

const PRESETS: { preset: RangePreset; labelKey: 'reports.range.today' | 'reports.range.7d' | 'reports.range.30d' }[] = [
  { preset: 'today', labelKey: 'reports.range.today' },
  { preset: '7d', labelKey: 'reports.range.7d' },
  { preset: '30d', labelKey: 'reports.range.30d' }
];

export function DateRangeControl({ value, onChange }: { value: DateRange; onChange: (range: DateRange) => void }) {
  const { t } = useI18n();
  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="flex gap-2">
        {PRESETS.map(p => (
          <Button key={p.preset} variant="outline" size="sm"
            onClick={() => onChange(presetRange(p.preset, new Date()))}>
            {t(p.labelKey)}
          </Button>
        ))}
      </div>
      <label className="flex flex-col gap-1 text-xs text-muted-foreground">
        {t('reports.range.from')}
        <Input type="date" aria-label={t('reports.range.from')} value={isoToDateInput(value.fromUtc)}
          onChange={e => onChange({ fromUtc: dateInputToFromUtc(e.target.value), toUtc: value.toUtc })} />
      </label>
      <label className="flex flex-col gap-1 text-xs text-muted-foreground">
        {t('reports.range.to')}
        <Input type="date" aria-label={t('reports.range.to')} value={isoToDateInput(value.toUtc)}
          onChange={e => onChange({ fromUtc: value.fromUtc, toUtc: dateInputToToUtc(e.target.value) })} />
      </label>
    </div>
  );
}
