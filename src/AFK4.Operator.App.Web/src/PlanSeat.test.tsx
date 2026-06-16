import { describe, expect, it } from 'bun:test';
import { render, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PlanSeat } from './PlanSeat';
import type { PlanSeat as PlanSeatModel } from './floorPlanState';

function model(overrides: Partial<PlanSeatModel> = {}): PlanSeatModel {
  return {
    id: 'pc-01',
    name: 'PC-01',
    tone: 'active',
    stateLabel: 'В сессии',
    seatType: 'pc',
    rotation: 0,
    posX: 2,
    posY: 3,
    ...overrides
  };
}

function renderSeat(seat: PlanSeatModel, props: { selected?: boolean; onSelect?: () => void } = {}) {
  return render(
    <I18nProvider>
      <PlanSeat seat={seat} cellSize={56} selected={props.selected} onSelect={props.onSelect ?? (() => {})} />
    </I18nProvider>
  );
}

describe('PlanSeat', () => {
  it('positions the seat by its grid cell and labels it by name and status', () => {
    const { getByRole } = renderSeat(model());
    const button = getByRole('button');
    expect(button.style.left).toBe(`${2 * 56}px`);
    expect(button.style.top).toBe(`${3 * 56}px`);
    expect(button.getAttribute('aria-label')).toBe('PC-01 В сессии');
    expect(button.className).toContain('state-active');
  });

  it('marks the selected seat and fires onSelect on click', () => {
    let clicked = false;
    const { getByRole } = renderSeat(model(), { selected: true, onSelect: () => { clicked = true; } });
    const button = getByRole('button');
    expect(button.className).toContain('selected');
    fireEvent.click(button);
    expect(clicked).toBe(true);
  });
});
