import { CircleDollarSign, LockKeyhole } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { OperatorRealtimeConnectionState } from './operatorRealtime';
import { dataSourceLabel, realtimeLabel } from './operatorHelpers';

// Нижний статус-бар: слева — связь, источник данных и касса; справа — разовый фид-бэк действия.
// Тон точки связи кодирует только severity.
export function ShellStatusBar({
  realtimeState,
  realtimeError,
  dataSource,
  workspaceFeedback,
  posText
}: {
  realtimeState: OperatorRealtimeConnectionState;
  realtimeError: string | null;
  dataSource: string;
  workspaceFeedback: string | null;
  posText: string;
}) {
  const { t } = useI18n();
  const networkTone = realtimeState === 'connected'
    ? 'ok'
    : realtimeState === 'connecting' || realtimeState === 'reconnecting'
      ? 'warn'
      : 'bad';
  // Точка краснеет, если сеть потеряна ИЛИ сервер не на связи (данные локальные, не из backend).
  // Переходные состояния сети (подключение/переподключение) остаются жёлтыми — это процесс, не сбой.
  const connectionTone = dataSource === 'backend' ? networkTone : 'bad';
  const realtimeText = realtimeLabel(realtimeState, realtimeError, t);
  const dataSourceText = dataSourceLabel(dataSource, t);
  return (
    <footer className="signals-strip">
      {/* Один индикатор связи: точка severity + статус сети. Источник данных скрыт в подсказке —
          по наведению видно оба статуса целиком, не загромождая бар. */}
      <span className="signal-item signal-conn" title={`${realtimeText} · ${dataSourceText}`}>
        <i className={`signal-dot ${connectionTone}`} aria-hidden="true" />
        {realtimeText}
      </span>
      <span className="signal-item signal-pos"><CircleDollarSign size={13} />{posText}</span>
      {workspaceFeedback && (
        <span className="signal-item rail-feedback signals-right"><LockKeyhole size={13} />{workspaceFeedback}</span>
      )}
    </footer>
  );
}
