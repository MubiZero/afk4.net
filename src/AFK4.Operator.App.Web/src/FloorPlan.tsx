import { useEffect, useRef, useState } from 'react';
import type { MouseEvent as ReactMouseEvent, PointerEvent as ReactPointerEvent } from 'react';
import { Minus, Plus } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { cellToPx, pxToCell, isCellOccupied, CANVAS_PADDING, DEFAULT_CELL_SIZE } from './floorPlanGeometry';
import type { PlanModel } from './floorPlanState';
import { PlanSeat } from './PlanSeat';

const MIN_SCALE = 0.4;
const MAX_SCALE = 2.5;

// Top-down floor-plan canvas: an SVG underlay (grid, zones, walls) with a DOM layer of seat markers
// on top. Pan by dragging empty space, zoom with the wheel or the +/- buttons.
// In edit mode (mode='edit') seats become draggable — drop onto a free cell fires onSeatMove.
// Assumes a non-empty model — the caller renders the «зал ещё не размечен» empty state instead.
export function FloorPlan({
  model,
  selectedSeatId,
  onSelectSeat,
  onSeatContextMenu,
  mode = 'view',
  onSeatMove,
  selectedZoneId
}: {
  model: PlanModel;
  selectedSeatId: string;
  onSelectSeat: (seatId: string) => void;
  onSeatContextMenu?: (seatId: string, event: ReactMouseEvent) => void;
  mode?: 'view' | 'edit';
  onSeatMove?: (seatId: string, posX: number, posY: number) => void;
  selectedZoneId?: string;
}) {
  const { t } = useI18n();
  const [scale, setScale] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [drag, setDrag] = useState<{ startX: number; startY: number; panX: number; panY: number } | null>(null);

  // Edit-mode state: which seat is currently being dragged (null = no drag).
  const [draggingSeatId, setDraggingSeatId] = useState<string | null>(null);

  // We need scale/originX/originY inside the window pointer listeners. Keep refs to avoid
  // stale closures — the effect re-runs only when draggingSeatId changes (the listener lifetime).
  const scaleRef = useRef(scale);
  scaleRef.current = scale;

  const viewportRef = useRef<HTMLDivElement>(null);
  // Invariant: the caller renders the empty state when isEmpty, so bbox is non-null here. The
  // fallback only guards a misuse and yields a harmless 1×1 canvas rather than crashing.
  const bbox = model.bbox ?? { minX: 0, minY: 0, maxX: 1, maxY: 1 };
  const cell = DEFAULT_CELL_SIZE;
  const originX = bbox.minX;
  const originY = bbox.minY;
  const widthPx = cellToPx(bbox.maxX - originX, cell) + CANVAS_PADDING * 2;
  const heightPx = cellToPx(bbox.maxY - originY, cell) + CANVAS_PADDING * 2;
  // Map an absolute cell to a pixel inside the padded canvas (origin shifted so nothing is negative).
  const px = (c: number) => cellToPx(c - originX, cell) + CANVAS_PADDING;
  const py = (c: number) => cellToPx(c - originY, cell) + CANVAS_PADDING;

  const originXRef = useRef(originX);
  originXRef.current = originX;
  const originYRef = useRef(originY);
  originYRef.current = originY;

  const modelRef = useRef(model);
  modelRef.current = model;
  const onSeatMoveRef = useRef(onSeatMove);
  onSeatMoveRef.current = onSeatMove;

  const gridLines: { key: string; x1: number; y1: number; x2: number; y2: number }[] = [];
  for (let x = originX; x <= bbox.maxX; x += 1) {
    gridLines.push({ key: `v-${x}`, x1: px(x), y1: py(originY), x2: px(x), y2: py(bbox.maxY) });
  }
  for (let y = originY; y <= bbox.maxY; y += 1) {
    gridLines.push({ key: `h-${y}`, x1: px(originX), y1: py(y), x2: px(bbox.maxX), y2: py(y) });
  }

  // Buttons jump twice the wheel step (0.2 vs 0.1) so discrete clicks reach the limits in fewer presses.
  const zoom = (delta: number) => setScale((s) => Math.min(MAX_SCALE, Math.max(MIN_SCALE, s + delta)));

  // Wheel zoom is wired as a native non-passive listener so preventDefault reliably stops the page
  // from scrolling (a React onWheel handler is passive by default in Chromium/WebView2).
  useEffect(() => {
    const node = viewportRef.current;
    if (node === null) {
      return;
    }

    const handleWheel = (event: WheelEvent) => {
      event.preventDefault();
      setScale((s) => Math.min(MAX_SCALE, Math.max(MIN_SCALE, s + (event.deltaY < 0 ? 0.1 : -0.1))));
    };

    node.addEventListener('wheel', handleWheel, { passive: false });
    return () => node.removeEventListener('wheel', handleWheel);
  }, []);

  // Edit mode: attach global pointer move/up listeners while a seat drag is active.
  // Using window listeners (not React synthetic events on the canvas) so the pointer can move
  // fast outside the element without losing events — mirrors how the wheel listener is wired.
  useEffect(() => {
    if (draggingSeatId === null) {
      return;
    }

    const computeTargetCell = (clientX: number, clientY: number): { x: number; y: number } => {
      // Get the canvas bounding rect. In jsdom it returns {left:0,top:0} so tests work naturally.
      const rect = viewportRef.current?.getBoundingClientRect() ?? { left: 0, top: 0 };

      // Step 1: pointer position relative to the viewport's top-left corner.
      const relX = clientX - rect.left;
      const relY = clientY - rect.top;

      // Step 2: divide by scale — the canvas is CSS-transformed with scale(s), so 1 CSS pixel
      // inside the canvas covers `scale` screen pixels. Without this division the snap target
      // drifts from the real cell whenever scale ≠ 1.
      const canvasX = relX / scaleRef.current;
      const canvasY = relY / scaleRef.current;

      // Step 3: subtract CANVAS_PADDING to get an origin-normalized pixel offset,
      // then convert to an integer cell index.
      const cellRelX = pxToCell(canvasX - CANVAS_PADDING, cell);
      const cellRelY = pxToCell(canvasY - CANVAS_PADDING, cell);

      // Step 4: add the layout origin so the result is an ABSOLUTE grid coordinate.
      // Without this addition the target would be off when the layout doesn't start at (0,0).
      return { x: cellRelX + originXRef.current, y: cellRelY + originYRef.current };
    };

    const handlePointerUp = (event: PointerEvent) => {
      const { x, y } = computeTargetCell(event.clientX, event.clientY);
      const seats = modelRef.current.placedSeats;
      if (!isCellOccupied(seats, x, y, draggingSeatId)) {
        onSeatMoveRef.current?.(draggingSeatId, x, y);
      }
      setDraggingSeatId(null);
    };

    window.addEventListener('pointerup', handlePointerUp);
    return () => {
      window.removeEventListener('pointerup', handlePointerUp);
    };
  }, [draggingSeatId, cell]);

  // Pan only when the drag starts on empty canvas (not on a seat marker — those handle their own click).
  // Capture the pointer so a fast drag that leaves the viewport keeps delivering move/up events here.
  const onPointerDown = (event: ReactPointerEvent) => {
    if ((event.target as HTMLElement).closest('.plan-seat')) {
      return;
    }
    event.currentTarget.setPointerCapture(event.pointerId);
    setDrag({ startX: event.clientX, startY: event.clientY, panX: pan.x, panY: pan.y });
  };
  const onPointerMove = (event: ReactPointerEvent) => {
    if (drag === null) {
      return;
    }
    setPan({ x: drag.panX + (event.clientX - drag.startX), y: drag.panY + (event.clientY - drag.startY) });
  };
  const endDrag = (event: ReactPointerEvent) => {
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    setDrag(null);
  };

  return (
    <div className="floor-plan">
      <div className="floor-plan-toolbar">
        <button type="button" aria-label={t('op.map.plan.zoomOut')} onClick={() => zoom(-0.2)}><Minus size={16} /></button>
        <button type="button" aria-label={t('op.map.plan.zoomReset')} onClick={() => { setScale(1); setPan({ x: 0, y: 0 }); }}>{Math.round(scale * 100)}%</button>
        <button type="button" aria-label={t('op.map.plan.zoomIn')} onClick={() => zoom(0.2)}><Plus size={16} /></button>
      </div>
      <div
        ref={viewportRef}
        className="floor-plan-viewport"
        role="application"
        aria-label={t('op.map.plan.canvasLabel')}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
      >
        <div
          className="floor-plan-canvas"
          style={{ width: `${widthPx}px`, height: `${heightPx}px`, transform: `translate(${pan.x}px, ${pan.y}px) scale(${scale})` }}
        >
          <svg className="floor-plan-underlay" width={widthPx} height={heightPx} aria-hidden="true">
            <g className="floor-plan-grid">
              {gridLines.map((line) => (
                <line key={line.key} x1={line.x1} y1={line.y1} x2={line.x2} y2={line.y2} />
              ))}
            </g>
            {model.zones.map((zone) => (
              <rect
                key={zone.id}
                className={zone.id === selectedZoneId ? 'floor-plan-zone floor-plan-zone--selected' : 'floor-plan-zone'}
                rx={8}
                x={px(zone.geoX)}
                y={py(zone.geoY)}
                width={cellToPx(zone.geoWidth, cell)}
                height={cellToPx(zone.geoHeight, cell)}
                style={zone.color ? { stroke: zone.color, fill: `color-mix(in srgb, ${zone.color} 12%, transparent)` } : undefined}
              />
            ))}
            {model.walls.map((wall) => (
              <line key={wall.id} className="floor-plan-wall" x1={px(wall.x1)} y1={py(wall.y1)} x2={px(wall.x2)} y2={py(wall.y2)} />
            ))}
          </svg>
          {model.zones.map((zone) => (
            <span
              key={`label-${zone.id}`}
              className="floor-plan-zone-label"
              style={{ left: `${px(zone.geoX) + 6}px`, top: `${py(zone.geoY) + 4}px` }}
            >
              {zone.name}
            </span>
          ))}
          <div className="floor-plan-seats" style={{ left: `${CANVAS_PADDING}px`, top: `${CANVAS_PADDING}px` }}>
            {model.placedSeats.map((seat) => (
              <PlanSeat
                key={seat.id}
                seat={{ ...seat, posX: seat.posX - originX, posY: seat.posY - originY }}
                cellSize={cell}
                selected={seat.id === selectedSeatId}
                onSelect={() => onSelectSeat(seat.id)}
                onContextMenu={onSeatContextMenu ? (event) => onSeatContextMenu(seat.id, event) : undefined}
                draggable={mode === 'edit'}
                onSeatDragStart={mode === 'edit' ? setDraggingSeatId : undefined}
              />
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
