import { useI18n } from '@afk4/i18n';
import { MonitorCheck, TriangleAlert } from 'lucide-react';
import type { AlertSource, MapFilterId } from './operatorTypes';

interface Props {
  // Разбивка критических состояний зала по типам (нет связи / сбой / обслуживание).
  // Клик по счётчику ведёт на карту, отфильтрованную точно на эти места.
  sources?: AlertSource[];
  onSelectSource?: (filterId: MapFilterId) => void;
}

// Статус-бар здоровья парка: либо разбивка критических состояний по типам (каждый счётчик
// кликабелен), либо позитивное «Всё в норме». Без общего агрегата «N проблем · M офлайн» —
// он дублировал бы те же офлайн-места, что уже считает чип «Нет связи».
export function ShellAlerts({ sources = [], onSelectSource }: Props) {
  const { t } = useI18n();
  const hasCritical = sources.length > 0;
  // Icon + text carry the meaning so the red is decoration, not the only signal.
  const Icon = hasCritical ? TriangleAlert : MonitorCheck;
  return (
    <span
      className={hasCritical ? 'shell-alerts danger' : 'shell-alerts'}
      aria-label={t('op.alerts.label')}
    >
      <Icon size={14} aria-hidden="true" />
      {hasCritical ? (
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
      ) : (
        t('op.alerts.allClear')
      )}
    </span>
  );
}
