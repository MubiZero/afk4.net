import { useI18n } from '@afk4/i18n';

export interface FloorPaletteItem {
  id: string;
  name: string;
  seatType: string;
}

export function FloorPalette({
  unplaced,
  onPlaceSeat
}: {
  unplaced: FloorPaletteItem[];
  onPlaceSeat: (id: string) => void;
}) {
  const { t } = useI18n();

  return (
    <div className="floor-palette">
      <h3 className="floor-palette-title">{t('op.map.plan.edit.paletteTitle')}</h3>
      {unplaced.length === 0 ? (
        <p className="floor-palette-empty">{t('op.map.plan.edit.paletteEmpty')}</p>
      ) : (
        <ul className="floor-palette-list">
          {unplaced.map((seat) => (
            <li key={seat.id}>
              <button
                type="button"
                className="floor-palette-item"
                onClick={() => onPlaceSeat(seat.id)}
              >
                {seat.name}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
