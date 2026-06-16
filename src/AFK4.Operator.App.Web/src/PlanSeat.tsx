import type { CSSProperties, MouseEvent as ReactMouseEvent, PointerEvent as ReactPointerEvent } from 'react';
import { cellToPx } from './floorPlanGeometry';
import type { PlanSeat as PlanSeatModel } from './floorPlanState';

// One seat on the plan canvas. Reuses the tone language of the grid tile (`state-${tone}`), but is a
// compact positioned marker. Rotation is applied visually via an inline transform. In edit mode
// (draggable=true) the seat captures pointerdown so it doesn't bubble up to the canvas pan handler.
export function PlanSeat({
  seat,
  cellSize,
  selected,
  onSelect,
  onContextMenu,
  draggable,
  onSeatDragStart
}: {
  seat: PlanSeatModel;
  cellSize: number;
  selected?: boolean;
  onSelect: () => void;
  onContextMenu?: (event: ReactMouseEvent) => void;
  draggable?: boolean;
  onSeatDragStart?: (seatId: string) => void;
}) {
  const className = [
    'plan-seat',
    `state-${seat.tone}`,
    selected ? 'selected' : '',
    draggable ? 'plan-seat--draggable' : ''
  ].filter(Boolean).join(' ');

  const style: CSSProperties = {
    left: `${cellToPx(seat.posX, cellSize)}px`,
    top: `${cellToPx(seat.posY, cellSize)}px`,
    transform: `rotate(${seat.rotation}deg)`
  };

  function handlePointerDown(event: ReactPointerEvent<HTMLButtonElement>) {
    if (!draggable) return;
    event.stopPropagation();
    onSeatDragStart?.(seat.id);
  }

  return (
    <button
      type="button"
      className={className}
      style={style}
      aria-label={`${seat.name} ${seat.stateLabel}`}
      aria-pressed={selected}
      onClick={onSelect}
      onContextMenu={onContextMenu}
      onPointerDown={handlePointerDown}
    >
      <span className="plan-seat-dot" aria-hidden="true" />
      <span className="plan-seat-name">{seat.name}</span>
    </button>
  );
}
