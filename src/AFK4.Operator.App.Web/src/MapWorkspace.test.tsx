import { describe, expect, it } from 'bun:test';
import { render, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { MapWorkspace } from './MapWorkspace';
import { createFixtureFloorMapState } from './floorMapState';

function renderWorkspace() {
  return render(
    <I18nProvider>
      <MapWorkspace
        floorMap={createFixtureFloorMapState()}
        session={null}
        actionsEnabled={false}
        selectedSeatId=""
        activeFilter="all"
        offlineActionAudit={[]}
        onSelectSeat={() => {}}
        onFilterChange={() => {}}
        onPcControlAction={async () => ({ detail: '' })}
        onSeatAction={async () => ({})}
      />
    </I18nProvider>
  );
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
    // Fixtures carry no coordinates → plan is empty.
    expect(getByText('Зал ещё не размечен')).not.toBeNull();
  });
});
