import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import { PanelSelect } from './PanelSelect';

export interface FloorInspectorSeat {
  id: string;
  name: string;
  seatType: string;
  rotation: number;
  posX: number;
  posY: number;
}

// Explicit literal array so each labelKey is a known MessageKey — template literals
// can't be type-checked against the union.
const SEAT_TYPE_OPTIONS: { value: string; labelKey: MessageKey }[] = [
  { value: 'pc',        labelKey: 'op.map.plan.edit.seatType.pc' },
  { value: 'console',   labelKey: 'op.map.plan.edit.seatType.console' },
  { value: 'vr',        labelKey: 'op.map.plan.edit.seatType.vr' },
  { value: 'sim',       labelKey: 'op.map.plan.edit.seatType.sim' },
  { value: 'billiard',  labelKey: 'op.map.plan.edit.seatType.billiard' },
  { value: 'boardgame', labelKey: 'op.map.plan.edit.seatType.boardgame' },
  { value: 'counter',   labelKey: 'op.map.plan.edit.seatType.counter' },
];

export function FloorInspector({
  seat,
  onRotate,
  onSetType,
  onRemove
}: {
  seat: FloorInspectorSeat;
  onRotate: (next: number) => void;
  onSetType: (type: string) => void;
  onRemove: (id: string) => void;
}) {
  const { t } = useI18n();

  const typeOptions = SEAT_TYPE_OPTIONS.map((o) => ({ value: o.value, label: t(o.labelKey) }));

  return (
    <div className="floor-inspector">
      <h3 className="floor-inspector-title">{t('op.map.plan.edit.inspectorTitle')}</h3>
      <p className="floor-inspector-name">{seat.name}</p>

      <div className="floor-inspector-field">
        <span className="floor-inspector-label">{t('op.map.plan.edit.seatTypeLabel')}</span>
        <PanelSelect
          value={seat.seatType}
          options={typeOptions}
          onChange={onSetType}
          ariaLabel={t('op.map.plan.edit.seatTypeLabel')}
        />
      </div>

      <div className="floor-inspector-actions">
        <button
          type="button"
          onClick={() => onRotate((seat.rotation + 90) % 360)}
        >
          {t('op.map.plan.edit.rotate')}
        </button>
        <button
          type="button"
          className="danger"
          onClick={() => onRemove(seat.id)}
        >
          {t('op.map.plan.edit.removeFromPlan')}
        </button>
      </div>
    </div>
  );
}
