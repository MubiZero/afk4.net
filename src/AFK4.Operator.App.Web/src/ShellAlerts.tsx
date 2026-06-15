import { useI18n } from '@afk4/i18n';
import { MonitorCheck, TriangleAlert } from 'lucide-react';

// Seam for the «Карта» stage: once map-driven alert feeds land they'll arrive as a list of
// typed sources. Declared now so the status-bar element's shape is stable, but intentionally
// not consumed yet (YAGNI) — wiring it before there's a producer would be dead code.
export interface AlertSource {
  id: string;
  tone: 'warning' | 'danger';
  label: string;
}

interface Props {
  problems: number;
  offline: number;
  sources?: AlertSource[]; // reserved for the «Карта» stage; do not consume yet
}

export function ShellAlerts({ problems, offline }: Props) {
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
    </span>
  );
}
