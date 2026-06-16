import { useMemo, useState } from 'react';
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
  toBulkUpdateRequest,
  type PlanDraft
} from './floorPlanDraft';
import type { FloorMapBulkUpdateRequest } from './api/clients/floorMap';
import { FloorPlan } from './FloorPlan';
import { FloorPalette } from './FloorPalette';
import { FloorInspector } from './FloorInspector';
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
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [saving, setSaving] = useState(false);
  const [confirmDiscard, setConfirmDiscard] = useState(false);

  const planModel = useMemo(() => planModelFromDraft(draft), [draft]);
  const unplaced = draft.seats
    .filter((seat) => seat.posX == null || seat.posY == null)
    .map((seat) => ({ id: seat.id, name: seat.name, seatType: seat.seatType }));
  const selectedSeat = draft.seats.find((seat) => seat.id === selectedSeatId) ?? null;
  const inspectorSeat = selectedSeat && selectedSeat.posX != null && selectedSeat.posY != null
    ? { id: selectedSeat.id, name: selectedSeat.name, seatType: selectedSeat.seatType, rotation: selectedSeat.rotation, posX: selectedSeat.posX, posY: selectedSeat.posY }
    : null;

  const handlePlaceSeat = (seatId: string) => {
    const cell = firstFreeCell(draft.seats);
    setDraft((current) => placeSeat(current, seatId, cell.x, cell.y));
    setSelectedSeatId(seatId);
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
        <FloorPalette unplaced={unplaced} onPlaceSeat={handlePlaceSeat} />
        <FloorPlan
          model={planModel}
          mode="edit"
          selectedSeatId={selectedSeatId}
          onSelectSeat={setSelectedSeatId}
          onSeatMove={(seatId, x, y) => setDraft((current) => moveSeat(current, seatId, x, y))}
        />
        {inspectorSeat && (
          <FloorInspector
            seat={inspectorSeat}
            onRotate={(next) => setDraft((current) => rotateSeat(current, inspectorSeat.id, next))}
            onSetType={(type) => setDraft((current) => setSeatType(current, inspectorSeat.id, type))}
            onRemove={(id) => { setDraft((current) => removeSeatFromPlan(current, id)); setSelectedSeatId(''); }}
          />
        )}
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
