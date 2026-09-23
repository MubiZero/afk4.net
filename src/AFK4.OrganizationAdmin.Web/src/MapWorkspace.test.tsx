import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render } from '@testing-library/react';
import { I18nProvider, createTranslator } from '@afk4/i18n';
import { MapWorkspace } from './MapWorkspace';
import { createFixtureFloorMapState } from './floorMapState';
import { ToastProvider } from './operatorToast';
import type { SeatSummary } from './operatorData';

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
    onResolveAssistance: async () => ({ detail: '' }),
    onSeatAction: async () => ({}),
    ...overrides
  };
  return render(
    <I18nProvider>
      <ToastProvider>
        <MapWorkspace {...props} />
      </ToastProvider>
    </I18nProvider>
  );
}

afterEach(cleanup);

describe('MapWorkspace', () => {
  // The «План» view is parked for now: only the grid is offered. Guards against the view switch
  // sneaking back in unintentionally — re-enabling the plan is a deliberate change, not an accident.
  it('renders the grid board and no longer offers a Plan (or Table) view switch', () => {
    const { getByRole, queryByText } = renderWorkspace();
    expect(getByRole('region', { name: t('op.map.seatsLabel') })).not.toBeNull();
    expect(queryByText('План')).toBeNull();
    expect(queryByText('Таблица')).toBeNull();
  });

  // Отбор, под который не подошёл ни один ПК, снимается кнопкой прямо на пустой карте.
  it('an empty filter offers «Сбросить фильтр», which returns to all PCs', () => {
    const onFilterChange = mock(() => {});
    const readySeat: SeatSummary = {
      id: 's1', zone: 'Зал A', name: 'PC-01', tone: 'ready', stateLabel: 'Свободно', player: 'Гость',
      remaining: 'Свободно', device: 'Device', command: 'Idle', app: 'Shell'
    };
    const { getByText, getByRole } = renderWorkspace({
      floorMap: { ...createFixtureFloorMapState(), seats: [readySeat], loadStatus: 'ready', source: 'backend' },
      activeFilter: 'offline',
      onFilterChange
    });
    expect(getByText(t('op.map.emptyTitle'))).not.toBeNull();
    fireEvent.click(getByRole('button', { name: t('op.empty.resetFilter') }));
    expect(onFilterChange).toHaveBeenCalledWith('all');
  });

  // Пустой филиал — не «смените фильтр»: ПК на карте появляются из Управления и Мастера настройки.
  it('a branch with no PCs says where seats and PCs come from, with no button', () => {
    const { getByText, container } = renderWorkspace({ floorMap: { ...createFixtureFloorMapState(), loadStatus: 'ready', source: 'backend' } });
    expect(getByText(t('op.map.noSeats.emptyTitle'))).not.toBeNull();
    expect(getByText(t('op.map.noSeats.emptyHint'))).not.toBeNull();
    expect(container.querySelector('.empty-state button')).toBeNull();
  });
});
