import { describe, expect, it } from 'bun:test';
import { render, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorPlan } from './FloorPlan';
import type { PlanModel } from './floorPlanState';

function nonEmptyModel(): PlanModel {
  return {
    placedSeats: [
      { id: 'pc-01', name: 'PC-01', tone: 'active', stateLabel: 'В сессии', seatType: 'pc', rotation: 0, posX: 1, posY: 1 },
      { id: 'pc-02', name: 'PC-02', tone: 'ready', stateLabel: 'Свободно', seatType: 'pc', rotation: 0, posX: 3, posY: 1 }
    ],
    unplacedSeats: [],
    zones: [
      { id: 'z1', name: 'Зал A', geoX: 0, geoY: 0, geoWidth: 5, geoHeight: 3, color: '#22d3ee', zoneType: 'hall' }
    ],
    walls: [{ id: 'w1', x1: 0, y1: 0, x2: 5, y2: 0 }],
    bbox: { minX: 0, minY: 0, maxX: 5, maxY: 3 },
    isEmpty: false
  };
}

function renderPlan(model: PlanModel, onSelectSeat: (id: string) => void = () => {}) {
  return render(
    <I18nProvider>
      <FloorPlan model={model} selectedSeatId="pc-01" onSelectSeat={onSelectSeat} />
    </I18nProvider>
  );
}

describe('FloorPlan edit mode', () => {
  it('view mode keeps seats non-draggable (regression)', () => {
    const { container } = render(
      <I18nProvider>
        <FloorPlan model={nonEmptyModel()} selectedSeatId="pc-01" onSelectSeat={() => {}} />
      </I18nProvider>
    );
    expect(container.querySelector('.plan-seat--draggable')).toBeNull();
  });

  it('edit mode marks seats draggable', () => {
    const { container } = render(
      <I18nProvider>
        <FloorPlan model={nonEmptyModel()} selectedSeatId="pc-01" mode="edit" onSelectSeat={() => {}} onSeatMove={() => {}} />
      </I18nProvider>
    );
    expect(container.querySelector('.plan-seat--draggable')).not.toBeNull();
  });

  it('edit mode reports a snapped move to a free cell', () => {
    const moves: Array<[string, number, number]> = [];
    const { getByRole } = render(
      <I18nProvider>
        <FloorPlan model={nonEmptyModel()} selectedSeatId="pc-01" mode="edit" onSelectSeat={() => {}}
          onSeatMove={(id, x, y) => moves.push([id, x, y])} />
      </I18nProvider>
    );
    const seat = getByRole('button', { name: 'PC-01 В сессии' });   // seat at (1,1)
    fireEvent.pointerDown(seat);
    fireEvent.pointerMove(window, { clientX: 256, clientY: 144 });   // → cell (4,2), free
    fireEvent.pointerUp(window, { clientX: 256, clientY: 144 });
    expect(moves).toEqual([['pc-01', 4, 2]]);
  });

  it('edit mode rejects a drop on an occupied cell', () => {
    const moves: unknown[] = [];
    const { getByRole } = render(
      <I18nProvider>
        <FloorPlan model={nonEmptyModel()} selectedSeatId="pc-01" mode="edit" onSelectSeat={() => {}}
          onSeatMove={(...args) => moves.push(args)} />
      </I18nProvider>
    );
    const seat = getByRole('button', { name: 'PC-01 В сессии' });    // (1,1)
    fireEvent.pointerDown(seat);
    fireEvent.pointerMove(window, { clientX: 32 + 3*56, clientY: 32 + 1*56 }); // → (3,1) = pc-02, occupied
    fireEvent.pointerUp(window, { clientX: 32 + 3*56, clientY: 32 + 1*56 });
    expect(moves).toEqual([]);
  });
});

describe('FloorPlan', () => {
  it('marks the selected zone rect with a highlight class', () => {
    const model = {
      placedSeats: [{ id: 's1', name: 'PC-1', tone: 'ready' as const, stateLabel: 'Свободно', seatType: 'pc', rotation: 0, posX: 0, posY: 0 }],
      unplacedSeats: [],
      zones: [
        { id: 'zone-1', name: 'Зал', geoX: 0, geoY: 0, geoWidth: 4, geoHeight: 3, color: null, zoneType: null },
        { id: 'zone-2', name: 'VIP', geoX: 0, geoY: 4, geoWidth: 3, geoHeight: 2, color: null, zoneType: null }
      ],
      walls: [],
      bbox: { minX: 0, minY: 0, maxX: 4, maxY: 6 },
      isEmpty: false
    };
    const { container } = render(
      <I18nProvider>
        <FloorPlan model={model} selectedSeatId="" onSelectSeat={() => {}} selectedZoneId="zone-2" />
      </I18nProvider>
    );
    const selected = container.querySelectorAll('.floor-plan-zone--selected');
    expect(selected.length).toBe(1);
  });

  it('renders a seat marker per placed seat and labels the zone', () => {
    const { getByRole, getByText } = renderPlan(nonEmptyModel());
    expect(getByRole('button', { name: 'PC-01 В сессии' })).not.toBeNull();
    expect(getByRole('button', { name: 'PC-02 Свободно' })).not.toBeNull();
    expect(getByText('Зал A')).not.toBeNull();
  });

  it('fires onSelectSeat with the seat id on click', () => {
    let picked = '';
    const { getByRole } = renderPlan(nonEmptyModel(), (id) => { picked = id; });
    fireEvent.click(getByRole('button', { name: 'PC-02 Свободно' }));
    expect(picked).toBe('pc-02');
  });

  it('marks the selected seat', () => {
    const { getByRole } = renderPlan(nonEmptyModel());
    expect(getByRole('button', { name: 'PC-01 В сессии' }).className).toContain('selected');
  });

  it('zooms in via the toolbar button and returns to 100% on reset', () => {
    const { getByRole, getByText } = renderPlan(nonEmptyModel());
    expect(getByText('100%')).not.toBeNull();
    fireEvent.click(getByRole('button', { name: 'Приблизить' }));
    expect(getByText('120%')).not.toBeNull();
    fireEvent.click(getByRole('button', { name: 'Сбросить масштаб' }));
    expect(getByText('100%')).not.toBeNull();
  });
});
