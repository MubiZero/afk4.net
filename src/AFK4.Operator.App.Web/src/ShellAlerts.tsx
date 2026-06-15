import { useI18n } from '@afk4/i18n';
import { MonitorCheck, TriangleAlert } from 'lucide-react';
import type { AlertSource, MapFilterId } from './operatorTypes';

interface Props {
  problems: number;
  offline: number;
  // Счётчики критсостояний зала; клик ведёт на карту, отфильтрованную на эти места.
  sources?: AlertSource[];
  onSelectSource?: (filterId: MapFilterId) => void;
}

export function ShellAlerts({ problems, offline, sources = [], onSelectSource }: Props) {
  const { t } = useI18n();
  const hasProblems = problems > 0;
  // Encode state beyond color: a distinct icon + the ICU text carry the meaning for anyone who
  // can't perceive the danger tone (the red is decoration, not the signal).
  const Icon = hasProblems ? TriangleAlert : MonitorCheck;
  return (
    <span
      className={hasProblems ? 'shell-alerts danger' : 'shell-alerts'}
      aria-label={t('op.alerts.label')}
    >
      <Icon size={14} aria-hidden="true" />
      {t('op.alerts.summary', { problems, offline })}
      {sources.length > 0 && (
        <span className="shell-alert-counters">
          {sources.map((source) => (
            <button
              key={source.id}
              type="button"
              className={`shell-alert-chip ${source.tone}`}
              onClick={() => onSelectSource?.(source.filterId)}
              title={t('op.alerts.critical.jump', { label: source.label })}
            >
              <span>{source.label}</span>
              <strong>{source.count}</strong>
            </button>
          ))}
        </span>
      )}
    </span>
  );
}
