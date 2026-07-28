import { useI18n } from '@afk4/i18n';
import { LockKeyhole } from 'lucide-react';
import { dataSourceLabel, realtimeLabel } from './operatorHelpers';
import type { OperatorRealtimeConnectionState } from './operatorRealtime';
import { buildSystemStatusModel } from './systemStatusModel';
import { useMinuteClock } from './useMinuteClock';

export function ShellStatusBar({
  operatorName,
  roleNames,
  clubName,
  realtimeState,
  realtimeError,
  dataSource,
  appVersion,
  workspaceFeedback
}: {
  operatorName: string;
  roleNames: string[];
  clubName: string;
  realtimeState: OperatorRealtimeConnectionState;
  realtimeError: string | null;
  dataSource: string;
  appVersion: string;
  workspaceFeedback: string | null;
}) {
  const { locale, t } = useI18n();
  const model = buildSystemStatusModel({
    operatorName,
    roleNames,
    clubName,
    realtimeState,
    dataSource,
    appVersion
  }, t);
  const time = useMinuteClock(locale);
  const connectionTitle = `${realtimeLabel(realtimeState, realtimeError, t)} · ${dataSourceLabel(dataSource, t)}`;

  return (
    <footer className="signals-strip">
      <div className="signal-cluster signal-left">
        {model.left.map((field) => (
          <span
            className={`signal-field signal-${field.key}`}
            key={field.key}
            title={`${field.label}: ${field.value}`}
          >
            <span>{field.label}:</span>
            <strong>{field.value}</strong>
          </span>
        ))}
      </div>

      {workspaceFeedback && (
        <span className="signal-feedback">
          <LockKeyhole size={13} aria-hidden="true" />
          {workspaceFeedback}
        </span>
      )}

      <div className="signal-cluster signal-right">
        <span className={`signal-field tone-${model.connection.tone}`} title={connectionTitle}>
          <i className={`signal-dot ${model.connection.tone}`} aria-hidden="true" />
          {model.connection.value}
        </span>
        <span className={`signal-field tone-${model.server.tone}`}>
          <span>{t('op.status.server')}:</span>
          <strong>{model.server.value}</strong>
        </span>
        <span className="signal-field">
          <span>{t('op.status.version')}:</span>
          <strong>{model.version.value}</strong>
        </span>
        <time className="signal-field" dateTime={time}>{time}</time>
      </div>
    </footer>
  );
}
