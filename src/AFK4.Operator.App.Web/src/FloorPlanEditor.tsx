import { useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { PlatformApiError } from './platformApi';
import { projectOperatorError } from './apiErrors';
import type { OperatorFloorMapState } from './floorMapState';
import { planModelFromDraft } from './floorPlanState';
import {
  createDraft,
  moveSeat,
  placeSeat,
  removeSeatFromPlan,
  rotateSeat,
  setSeatType,
  setSeatZone,
  addZone,
  setZoneGeometry,
  renameZone,
  setZoneColor,
  removeZone,
  zoneSeatCount,
  toBulkUpdateRequest,
  type PlanDraft
} from './floorPlanDraft';
import type { FloorMapBulkUpdateRequest } from './api/clients/floorMap';
import { FloorPlan } from './FloorPlan';
import { FloorPalette } from './FloorPalette';
import { FloorZonePanel } from './FloorZonePanel';
import { FloorInspector } from './FloorInspector';
import { FloorZoneInspector } from './FloorZoneInspector';
import { PanelModal } from './PanelModal';
import { FeedbackNotice } from './operatorPrimitives';
import { emptyFeedback } from './operatorHelpers';
import type { Feedback } from './operatorTypes';

const PLACE_COLUMNS = 10;

// First free grid cell, filling left-to-right then down. Used when the palette places a seat that
// has no coordinates yet — we drop it on the first open cell rather than asking the operator to aim.
function firstFreeCell(placed: { posX: number | null; posY: number | null }[]): { x: number; y: number } {
  const taken = new Set(placed.filter((s) => s.posX != null && s.posY != null).map((s) => `${s.posX},${s.posY}`));
  for (let i = 0; ; i += 1) {
    const x = i % PLACE_COLUMNS;
    const y = Math.floor(i / PLACE_COLUMNS);
    if (!taken.has(`${x},${y}`)) {
      return { x, y };
    }
  }
}

// Edit-mode wrapper for the «План» view: holds a local layout draft, lets a manager place/move/
// rotate/retype seats, and saves the whole layout in one transaction. Live statuses are frozen
// while editing (we work off the snapshot taken on entry — arranging, not monitoring).
export function FloorPlanEditor({
  floorMap,
  organizationId,
  onSave,
  onExit
}: {
  floorMap: OperatorFloorMapState;
  organizationId: string;
  onSave: (request: FloorMapBulkUpdateRequest) => Promise<void>;
  onExit: () => void;
}) {
  const { t } = useI18n();
  const [draft, setDraft] = useState<PlanDraft>(() => createDraft(floorMap));
  const [selectedSeatId, setSelectedSeatId] = useState('');
  const [selectedZoneId, setSelectedZoneId] = useState('');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [saving, setSaving] = useState(false);
  const [confirmDiscard, setConfirmDiscard] = useState(false);
  // Monotonic counter for brand-new zones' client ids (never reused, so it can't collide with a
  // deleted zone's id within one editing session).
  const zoneSeq = useRef(0);

  // Selection is seat XOR zone — picking one clears the other so only one inspector shows.
  const selectSeat = (id: string) => { setSelectedSeatId(id); setSelectedZoneId(''); };
  const selectZone = (id: string) => { setSelectedZoneId(id); setSelectedSeatId(''); };

  const planModel = useMemo(() => planModelFromDraft(draft), [draft]);
  const unplaced = draft.seats
    .filter((seat) => seat.posX == null || seat.posY == null)
    .map((seat) => ({ id: seat.id, name: seat.name, seatType: seat.seatType }));
  const zoneItems = draft.zones.map((zone) => ({ id: zone.zoneId, name: zone.name }));
  const selectedSeat = draft.seats.find((seat) => seat.id === selectedSeatId) ?? null;
  const inspectorSeat = selectedSeat && selectedSeat.posX != null && selectedSeat.posY != null
    ? { id: selectedSeat.id, name: selectedSeat.name, seatType: selectedSeat.seatType, rotation: selectedSeat.rotation, posX: selectedSeat.posX, posY: selectedSeat.posY }
    : null;

  const selectedZone = draft.zones.find((zone) => zone.zoneId === selectedZoneId) ?? null;
  // A zone never arranged yet has null geometry — fall back to a 1×1 at the origin so the numeric
  // fields have real numbers to edit (editing them gives the zone a rectangle on the canvas).
  const inspectorZone = selectedZone
    ? selectedZone.geoX != null && selectedZone.geoY != null && selectedZone.geoWidth != null && selectedZone.geoHeight != null
      ? { id: selectedZone.zoneId, name: selectedZone.name, geoX: selectedZone.geoX, geoY: selectedZone.geoY, geoWidth: selectedZone.geoWidth, geoHeight: selectedZone.geoHeight, color: selectedZone.color }
      : { id: selectedZone.zoneId, name: selectedZone.name, geoX: 0, geoY: 0, geoWidth: 1, geoHeight: 1, color: selectedZone.color }
    : null;

  // Deleting is blocked while the zone still owns seats (their zoneClientId would dangle → backend 400),
  // and while it is the last zone (the backend requires ≥1). Explain which, so the button isn't a dead end.
  const deleteBlockedReason = selectedZone
    ? zoneSeatCount(draft, selectedZone.zoneId) > 0
      ? t('op.map.plan.edit.deleteZoneHasSeats')
      : draft.zones.length <= 1
        ? t('op.map.plan.edit.deleteZoneLast')
        : null
    : null;
  const canDeleteZone = selectedZone !== null && deleteBlockedReason === null;

  const handlePlaceSeat = (seatId: string) => {
    const cell = firstFreeCell(draft.seats);
    setDraft((current) => placeSeat(current, seatId, cell.x, cell.y));
    selectSeat(seatId);
  };

  const handleAddZone = () => {
    let bottom = 0;
    for (const zone of draft.zones) {
      if (zone.geoY != null && zone.geoHeight != null) bottom = Math.max(bottom, zone.geoY + zone.geoHeight);
    }
    for (const seat of draft.seats) {
      if (seat.posY != null) bottom = Math.max(bottom, seat.posY + 1);
    }
    const clientId = `new-zone-${zoneSeq.current++}`;
    setDraft((current) => addZone(current, clientId, t('op.map.plan.edit.newZoneName'), 0, bottom, 4, 3));
    selectZone(clientId);
  };

  const mapSaveError = (error: unknown): string => {
    if (error instanceof PlatformApiError) {
      // 412 (stale ETag) and 428 (no ETag) both mean "someone else changed the layout" to the operator.
      if (error.status === 412 || error.status === 428) {
        return t('op.map.plan.edit.conflict');
      }
    }
    // 409 (seat still has a device/history) and 400 carry a specific backend reason — surface it (#34).
    return projectOperatorError(error, t).detail || t('op.map.plan.edit.saveFailed');
  };

  const handleSave = async () => {
    setSaving(true);
    setFeedback(emptyFeedback);
    try {
      await onSave(toBulkUpdateRequest(draft, organizationId));
      onExit();
    } catch (error) {
      setFeedback({ label: t('op.map.plan.edit.saveFailed'), state: 'failed', detail: mapSaveError(error) });
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    if (draft.isDirty) {
      setConfirmDiscard(true);
      return;
    }
    onExit();
  };

  return (
    <div className="floor-plan-editor">
      <div className="floor-plan-editor-bar">
        <button type="button" className="primary" onClick={handleSave} disabled={saving}>
          {t('op.map.plan.edit.save')}
        </button>
        <button type="button" onClick={handleCancel} disabled={saving}>
          {t('op.map.plan.edit.cancel')}
        </button>
      </div>

      <FeedbackNotice feedback={feedback} />

      <div className="floor-plan-editor-body">
        <div className="floor-plan-editor-rail">
          <FloorPalette unplaced={unplaced} onPlaceSeat={handlePlaceSeat} />
          <FloorZonePanel
            zones={zoneItems}
            selectedZoneId={selectedZoneId}
            onSelectZone={selectZone}
            onAddZone={handleAddZone}
          />
        </div>
        <FloorPlan
          model={planModel}
          mode="edit"
          selectedSeatId={selectedSeatId}
          selectedZoneId={selectedZoneId}
          onSelectSeat={selectSeat}
          onSeatMove={(seatId, x, y) => setDraft((current) => moveSeat(current, seatId, x, y))}
        />
        {inspectorSeat ? (
          <FloorInspector
            seat={{ ...inspectorSeat, zoneId: selectedSeat?.zoneId ?? '' }}
            zones={zoneItems}
            onSetZone={(zoneId) => setDraft((current) => setSeatZone(current, inspectorSeat.id, zoneId))}
            onRotate={(next) => setDraft((current) => rotateSeat(current, inspectorSeat.id, next))}
            onSetType={(type) => setDraft((current) => setSeatType(current, inspectorSeat.id, type))}
            onRemove={(id) => { setDraft((current) => removeSeatFromPlan(current, id)); setSelectedSeatId(''); }}
          />
        ) : inspectorZone ? (
          <FloorZoneInspector
            zone={inspectorZone}
            canDelete={canDeleteZone}
            deleteBlockedReason={deleteBlockedReason}
            onRename={(name) => setDraft((current) => renameZone(current, inspectorZone.id, name))}
            onGeometry={(geo) => setDraft((current) => setZoneGeometry(current, inspectorZone.id, geo))}
            onColor={(color) => setDraft((current) => setZoneColor(current, inspectorZone.id, color))}
            onDelete={(id) => { setDraft((current) => removeZone(current, id)); setSelectedZoneId(''); }}
          />
        ) : null}
      </div>

      {confirmDiscard && (
        <PanelModal
          title={t('op.map.plan.edit.confirmDiscardTitle')}
          subtitle={t('op.map.plan.edit.confirmDiscardBody')}
          tone="warning"
          onClose={() => setConfirmDiscard(false)}
        >
          <div className="floor-plan-editor-confirm-actions">
            <button type="button" className="danger" onClick={() => { setConfirmDiscard(false); onExit(); }}>
              {t('op.map.plan.edit.confirmDiscardYes')}
            </button>
            <button type="button" onClick={() => setConfirmDiscard(false)}>
              {t('op.map.plan.edit.confirmKeep')}
            </button>
          </div>
        </PanelModal>
      )}
    </div>
  );
}
