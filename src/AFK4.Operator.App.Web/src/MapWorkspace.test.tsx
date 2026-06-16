import { describe, expect, it } from 'bun:test';
import { render, fireEvent } from '@testing-library/react';
import { I18nProvider, createTranslator } from '@afk4/i18n';
import { MapWorkspace } from './MapWorkspace';
import { createFixtureFloorMapState, mapFloorMapDtoToState, type OperatorFloorMapState } from './floorMapState';
import type { OperatorAuthSession } from './authClient';

const t = createTranslator('ru');

type WorkspaceProps = Parameters<typeof MapWorkspace>[0];

function renderWorkspace(overrides: Partial<WorkspaceProps> = {}) {
  const props: WorkspaceProps = {
    floorMap: createFixtureFloorMapState(),
    session: null,
    actionsEnabled: false,
    selectedSeatId: '',
    activeFilter: 'all',
    offlineActionAudit: [],
    onSelectSeat: () => {},
    onFilterChange: () => {},
    onPcControlAction: async () => ({ detail: '' }),
    onSeatAction: async () => ({}),
    onSaveLayout: async () => {},
    ...overrides
  };
  return render(
    <I18nProvider>
      <MapWorkspace {...props} />
    </I18nProvider>
  );
}

// A backend-loaded map with one unplaced seat → plan is empty but editable (has a real ETag).
function backendEmptyPlan(): OperatorFloorMapState {
  return mapFloorMapDtoToState({
    branchId: 'b1',
    branchName: 'AFK4 Dushanbe',
    zones: [{ zoneId: 'z1', name: 'Зал A', sortOrder: 0 }],
    walls: [],
    seats: [{
      seatId: 's1', seatName: 'PC-01', zoneId: 'z1', zoneName: 'Зал A', sortOrder: 0, state: 'available',
      deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null,
      agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null
    }]
  } as unknown as Parameters<typeof mapFloorMapDtoToState>[0], t, 'W/"v1"');
}

function session(permissions: string[]): OperatorAuthSession {
  return { permissions, organizationId: 'org-1' } as unknown as OperatorAuthSession;
}

describe('MapWorkspace view switch', () => {
  it('offers Grid and Plan views and no Table view', () => {
    const { getByText, queryByText } = renderWorkspace();
    expect(getByText('Карта')).not.toBeNull();
    expect(getByText('План')).not.toBeNull();
    expect(queryByText('Таблица')).toBeNull();
  });

  it('switches to the plan view and shows the not-arranged empty state for unplaced fixtures', () => {
    const { getByText } = renderWorkspace();
    fireEvent.click(getByText('План'));
    expect(getByText('Зал ещё не размечен')).not.toBeNull();
  });
});

describe('MapWorkspace layout editing', () => {
  it('shows «Разметить план» on an empty plan for a manager (layout.manage)', () => {
    const { getByText } = renderWorkspace({ floorMap: backendEmptyPlan(), session: session(['floor_map.view', 'layout.manage']) });
    fireEvent.click(getByText('План'));
    expect(getByText('Разметить план')).not.toBeNull();
  });

  it('hides the arrange entry point without layout.manage', () => {
    const { getByText, queryByText } = renderWorkspace({ floorMap: backendEmptyPlan(), session: session(['floor_map.view']) });
    fireEvent.click(getByText('План'));
    expect(getByText('Зал ещё не размечен')).not.toBeNull();
    expect(queryByText('Разметить план')).toBeNull();
  });
});
