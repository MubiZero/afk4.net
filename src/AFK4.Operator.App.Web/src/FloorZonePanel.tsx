import { useI18n } from '@afk4/i18n';
import { Plus } from 'lucide-react';

export interface FloorZonePanelItem {
  id: string;
  name: string;
}

export function FloorZonePanel({
  zones,
  selectedZoneId,
  onSelectZone,
  onAddZone
}: {
  zones: FloorZonePanelItem[];
  selectedZoneId: string;
  onSelectZone: (id: string) => void;
  onAddZone: () => void;
}) {
  const { t } = useI18n();

  return (
    <div className="floor-zone-panel">
      <h3 className="floor-palette-title">{t('op.map.plan.edit.zonesPanelTitle')}</h3>
      <ul className="floor-palette-list">
        {zones.map((zone) => (
          <li key={zone.id}>
            <button
              type="button"
              className={zone.id === selectedZoneId ? 'floor-palette-item is-selected' : 'floor-palette-item'}
              onClick={() => onSelectZone(zone.id)}
            >
              {zone.name}
            </button>
          </li>
        ))}
      </ul>
      <button type="button" className="floor-zone-add" onClick={onAddZone}>
        <Plus size={14} aria-hidden="true" /> {t('op.map.plan.edit.addZone')}
      </button>
    </div>
  );
}
