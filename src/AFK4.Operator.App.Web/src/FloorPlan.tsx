import { useState } from 'react';
import type { MouseEvent as ReactMouseEvent, PointerEvent as ReactPointerEvent, WheelEvent as ReactWheelEvent } from 'react';
import { Minus, Plus } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { cellToPx, CANVAS_PADDING, DEFAULT_CELL_SIZE } from './floorPlanGeometry';
import type { PlanModel } from './floorPlanState';
import { PlanSeat } from './PlanSeat';

const MIN_SCALE = 0.4;
const MAX_SCALE = 2.5;

// Top-down floor-plan canvas (read-only in B2-2): an SVG underlay (grid, zones, walls) with a DOM
// layer of seat markers on top. Pan by dragging empty space, zoom with the wheel or the +/- buttons.
// Assumes a non-empty model — the caller renders the «зал ещё не размечен» empty state instead.
export function FloorPlan({
  model,
  selectedSeatId,
  onSelectSeat,
  onSeatContextMenu
}: {
  model: PlanModel;
  selectedSeatId: string;
  onSelectSeat: (seatId: string) => void;
  onSeatContextMenu?: (seatId: string, event: ReactMouseEvent) => void;
}) {
  const { t } = useI18n();
  const [scale, setScale] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [drag, setDrag] = useState<{ startX: number; startY: number; panX: number; panY: number } | null>(null);

  const bbox = model.bbox ?? { minX: 0, minY: 0, maxX: 1, maxY: 1 };
  const cell = DEFAULT_CELL_SIZE;
  const originX = bbox.minX;
  const originY = bbox.minY;
  const widthPx = cellToPx(bbox.maxX - originX, cell) + CANVAS_PADDING * 2;
  const heightPx = cellToPx(bbox.maxY - originY, cell) + CANVAS_PADDING * 2;
  // Map an absolute cell to a pixel inside the padded canvas (origin shifted so nothing is negative).
  const px = (c: number) => cellToPx(c - originX, cell) + CANVAS_PADDING;
  const py = (c: number) => cellToPx(c - originY, cell) + CANVAS_PADDING;

  const gridLines: { key: string; x1: number; y1: number; x2: number; y2: number }[] = [];
  for (let x = originX; x <= bbox.maxX; x += 1) {
    gridLines.push({ key: `v-${x}`, x1: px(x), y1: py(originY), x2: px(x), y2: py(bbox.maxY) });
  }
  for (let y = originY; y <= bbox.maxY; y += 1) {
    gridLines.push({ key: `h-${y}`, x1: px(originX), y1: py(y), x2: px(bbox.maxX), y2: py(y) });
  }

  const zoom = (delta: number) => setScale((s) => Math.min(MAX_SCALE, Math.max(MIN_SCALE, s + delta)));

  const onWheel = (event: ReactWheelEvent) => {
    event.preventDefault();
    zoom(event.deltaY < 0 ? 0.1 : -0.1);
  };

  // Pan only when the drag starts on empty canvas (not on a seat marker — those handle their own click).
  const onPointerDown = (event: ReactPointerEvent) => {
    if ((event.target as HTMLElement).closest('.plan-seat')) {
      return;
    }
    setDrag({ startX: event.clientX, startY: event.clientY, panX: pan.x, panY: pan.y });
  };
  const onPointerMove = (event: ReactPointerEvent) => {
    if (drag === null) {
      return;
    }
    setPan({ x: drag.panX + (event.clientX - drag.startX), y: drag.panY + (event.clientY - drag.startY) });
  };
  const endDrag = () => setDrag(null);

  return (
    <div className="floor-plan">
      <div className="floor-plan-toolbar">
        <button type="button" aria-label={t('op.map.plan.zoomOut' as MessageKey)} onClick={() => zoom(-0.2)}><Minus size={16} /></button>
        <button type="button" aria-label={t('op.map.plan.zoomReset' as MessageKey)} onClick={() => { setScale(1); setPan({ x: 0, y: 0 }); }}>{Math.round(scale * 100)}%</button>
        <button type="button" aria-label={t('op.map.plan.zoomIn' as MessageKey)} onClick={() => zoom(0.2)}><Plus size={16} /></button>
      </div>
      <div
        className="floor-plan-viewport"
        role="application"
        aria-label={t('op.map.plan.canvasLabel' as MessageKey)}
        onWheel={onWheel}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endDrag}
        onPointerLeave={endDrag}
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
                className="floor-plan-zone"
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
              />
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
