import { useI18n } from '@afk4/i18n';

export interface FloorInspectorZone {
  id: string;
  name: string;
  geoX: number;
  geoY: number;
  geoWidth: number;
  geoHeight: number;
  color: string | null;
}

// Preset swatches — any CSS colour works on the canvas (rendered via color-mix), but a fixed
// palette keeps the look consistent and avoids shipping a full colour picker (YAGNI).
const COLOR_PRESETS = ['#3b82f6', '#22c55e', '#f59e0b', '#ef4444', '#a855f7', '#14b8a6'];

export function FloorZoneInspector({
  zone,
  canDelete,
  deleteBlockedReason,
  onRename,
  onGeometry,
  onColor,
  onDelete
}: {
  zone: FloorInspectorZone;
  canDelete: boolean;
  deleteBlockedReason: string | null;
  onRename: (name: string) => void;
  onGeometry: (geo: Partial<{ geoX: number; geoY: number; geoWidth: number; geoHeight: number }>) => void;
  onColor: (color: string | null) => void;
  onDelete: (id: string) => void;
}) {
  const { t } = useI18n();

  // Number inputs clamp at the field's floor; ignore non-numeric/empty input so the draft never goes NaN.
  const numberField = (
    label: string,
    value: number,
    floor: number,
    apply: (n: number) => void
  ) => (
    <label className="floor-inspector-field">
      <span className="floor-inspector-label">{label}</span>
      <input
        type="number"
        min={floor}
        value={value}
        aria-label={label}
        onChange={(event) => {
          const next = Number(event.target.value);
          if (Number.isFinite(next)) {
            apply(Math.max(floor, Math.round(next)));
          }
        }}
      />
    </label>
  );

  return (
    <div className="floor-inspector">
      <h3 className="floor-inspector-title">{t('op.map.plan.edit.zoneInspectorTitle')}</h3>

      <label className="floor-inspector-field">
        <span className="floor-inspector-label">{t('op.map.plan.edit.zoneNameLabel')}</span>
        <input
          type="text"
          value={zone.name}
          aria-label={t('op.map.plan.edit.zoneNameLabel')}
          onChange={(event) => onRename(event.target.value)}
        />
      </label>

      <div className="floor-zone-geo-grid">
        {numberField(t('op.map.plan.edit.zoneXLabel'), zone.geoX, 0, (n) => onGeometry({ geoX: n }))}
        {numberField(t('op.map.plan.edit.zoneYLabel'), zone.geoY, 0, (n) => onGeometry({ geoY: n }))}
        {numberField(t('op.map.plan.edit.zoneWidthLabel'), zone.geoWidth, 1, (n) => onGeometry({ geoWidth: n }))}
        {numberField(t('op.map.plan.edit.zoneHeightLabel'), zone.geoHeight, 1, (n) => onGeometry({ geoHeight: n }))}
      </div>

      <div className="floor-inspector-field">
        <span className="floor-inspector-label">{t('op.map.plan.edit.zoneColorLabel')}</span>
        <div className="floor-zone-swatches">
          {COLOR_PRESETS.map((preset) => (
            <button
              key={preset}
              type="button"
              className={zone.color === preset ? 'floor-zone-swatch is-selected' : 'floor-zone-swatch'}
              style={{ background: preset }}
              aria-label={preset}
              aria-pressed={zone.color === preset}
              onClick={() => onColor(preset)}
            />
          ))}
          <button type="button" className="floor-zone-swatch-none" onClick={() => onColor(null)}>
            {t('op.map.plan.edit.zoneColorNone')}
          </button>
        </div>
      </div>

      <div className="floor-inspector-actions">
        <button type="button" className="danger" disabled={!canDelete} onClick={() => onDelete(zone.id)}>
          {t('op.map.plan.edit.deleteZone')}
        </button>
        {!canDelete && deleteBlockedReason && (
          <p className="floor-zone-delete-reason">{deleteBlockedReason}</p>
        )}
      </div>
    </div>
  );
}
