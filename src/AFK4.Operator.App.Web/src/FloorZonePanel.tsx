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
      {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
      <h3 className="floor-palette-title">{(t as any)('op.map.plan.edit.zonesPanelTitle')}</h3>
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
      {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
      <button type="button" className="floor-zone-add" onClick={onAddZone}>
        <Plus size={14} aria-hidden="true" /> {(t as any)('op.map.plan.edit.addZone')}
      </button>
    </div>
  );
}
