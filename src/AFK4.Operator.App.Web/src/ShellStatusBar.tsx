import { CircleDollarSign, LockKeyhole } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { OperatorRealtimeConnectionState } from './operatorRealtime';
import { dataSourceLabel, realtimeLabel } from './operatorHelpers';

// Нижний статус-бар: слева — касса (и разовый фид-бэк действия), справа — связь с точкой-severity
// в самом углу. Тон точки связи кодирует только severity.
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
      {/* Касса — в левом углу. */}
      <span className="signal-item signal-pos"><CircleDollarSign size={13} />{posText}</span>
      {workspaceFeedback && (
        <span className="signal-item rail-feedback"><LockKeyhole size={13} />{workspaceFeedback}</span>
      )}
      {/* Связь — в правом углу: статус сети, а точка-severity в самом углу (после текста).
          Источник данных скрыт в подсказке — по наведению видно оба статуса целиком. */}
      <span className="signal-item signal-conn signals-right" title={`${realtimeText} · ${dataSourceText}`}>
        {realtimeText}
        <i className={`signal-dot ${connectionTone}`} aria-hidden="true" />
      </span>
    </footer>
  );
}
