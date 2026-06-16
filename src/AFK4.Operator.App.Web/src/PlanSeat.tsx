import type { CSSProperties, MouseEvent as ReactMouseEvent } from 'react';
import { cellToPx } from './floorPlanGeometry';
import type { PlanSeat as PlanSeatModel } from './floorPlanState';

// One seat on the plan canvas. Reuses the tone language of the grid tile (`state-${tone}`), but is a
// compact positioned marker. Rotation is carried in the model but not applied visually here — that
// lands in the editor (B2-3), so the label stays upright and readable in the read-only view.
export function PlanSeat({
  seat,
  cellSize,
  selected,
  onSelect,
  onContextMenu
}: {
  seat: PlanSeatModel;
  cellSize: number;
  selected?: boolean;
  onSelect: () => void;
  onContextMenu?: (event: ReactMouseEvent) => void;
}) {
  const className = ['plan-seat', `state-${seat.tone}`, selected ? 'selected' : ''].filter(Boolean).join(' ');
  const style: CSSProperties = {
    left: `${cellToPx(seat.posX, cellSize)}px`,
    top: `${cellToPx(seat.posY, cellSize)}px`
  };

  return (
    <button
      type="button"
      className={className}
      style={style}
      aria-label={`${seat.name} ${seat.stateLabel}`}
      aria-pressed={selected}
      onClick={onSelect}
      onContextMenu={onContextMenu}
    >
      <span className="plan-seat-dot" aria-hidden="true" />
      <span className="plan-seat-name">{seat.name}</span>
    </button>
  );
}
