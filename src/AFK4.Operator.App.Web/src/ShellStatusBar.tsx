import { CircleDollarSign, LockKeyhole } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from './operatorData';
import type { MapFilterId } from './operatorTypes';
import type { OperatorRealtimeConnectionState } from './operatorRealtime';
import { ShellAlerts } from './ShellAlerts';
import { criticalAlertSources, dataSourceLabel, realtimeLabel } from './operatorHelpers';

// Нижний статус-бар по единому стандарту: слева — система (связь, источник данных, тревоги),
// справа — бизнес-метрика (касса). Тон точки связи кодирует только severity.
export function ShellStatusBar({
  realtimeState,
  realtimeError,
  dataSource,
  seats,
  workspaceFeedback,
  posText,
  onSelectAlertSource
}: {
  realtimeState: OperatorRealtimeConnectionState;
  realtimeError: string | null;
  dataSource: string;
  seats: SeatSummary[];
  workspaceFeedback: string | null;
  posText: string;
  onSelectAlertSource: (filterId: MapFilterId) => void;
}) {
  const { t } = useI18n();
  const realtimeTone = realtimeState === 'connected'
    ? 'ok'
    : realtimeState === 'connecting' || realtimeState === 'reconnecting'
      ? 'warn'
      : 'bad';
  return (
    <footer className="signals-strip">
      <span className="signal-item">
        <i className={`signal-dot ${realtimeTone}`} aria-hidden="true" />
        {realtimeLabel(realtimeState, realtimeError, t)}
      </span>
      <span className="signal-item signal-muted">{dataSourceLabel(dataSource, t)}</span>
      <ShellAlerts sources={criticalAlertSources(seats, t)} onSelectSource={onSelectAlertSource} />
      <div className="signals-right">
        {workspaceFeedback && (
          <span className="signal-item rail-feedback"><LockKeyhole size={13} />{workspaceFeedback}</span>
        )}
        <span className="signal-item signal-pos"><CircleDollarSign size={13} />{posText}</span>
      </div>
    </footer>
  );
}
