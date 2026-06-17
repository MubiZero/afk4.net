import { describe, expect, it } from 'bun:test';
import { render } from '@testing-library/react';
import { I18nProvider, createTranslator } from '@afk4/i18n';
import { MapWorkspace } from './MapWorkspace';
import { createFixtureFloorMapState } from './floorMapState';

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
    ...overrides
  };
  return render(
    <I18nProvider>
      <MapWorkspace {...props} />
    </I18nProvider>
  );
}

describe('MapWorkspace', () => {
  // The «План» view is parked for now: only the grid is offered. Guards against the view switch
  // sneaking back in unintentionally — re-enabling the plan is a deliberate change, not an accident.
  it('renders the grid board and no longer offers a Plan (or Table) view switch', () => {
    const { getByRole, queryByText } = renderWorkspace();
    expect(getByRole('region', { name: t('op.map.seatsLabel') })).not.toBeNull();
    expect(queryByText('План')).toBeNull();
    expect(queryByText('Таблица')).toBeNull();
  });
});
