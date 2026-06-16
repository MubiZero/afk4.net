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

describe('FloorPlan', () => {
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
});
