import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, waitFor } from '@testing-library/react';
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

  // Шапка — как у остальных разделов: название раздела (не имя клуба, оно в нижней строке) и
  // фильтры-чипы, нажатый — aria-pressed, а не только цвет.
  it('heads the map with the section name and filter chips that report which one is on', () => {
    const onFilterChange = mock(() => {});
    const { getByRole, queryByText } = renderWorkspace({ activeFilter: 'ready', onFilterChange });
    expect(getByRole('heading', { level: 1 }).textContent).toBe(t('op.map.title'));
    expect(queryByText(createFixtureFloorMapState().branchName)).toBeNull();
    const filters = getByRole('group', { name: t('op.map.filterLabel') });
    const pressed = [...filters.querySelectorAll('button')].filter((button) => button.getAttribute('aria-pressed') === 'true');
    expect(pressed).toHaveLength(1);
    fireEvent.click(filters.querySelector('button')!);
    expect(onFilterChange).toHaveBeenCalledWith('all');
  });

  // «Выбрать зал» — переключатель у названия зала: нажатость говорит aria-pressed, подпись одна.
  it('toggles a whole hall for a PC command from the chip next to its name', () => {
    const base = { zone: 'Зал A', stateLabel: 'Свободно', player: 'Гость', remaining: 'Свободно', device: 'Device', command: 'Idle', app: 'Shell', isDeviceOnline: true, tone: 'ready' as const };
    const seats: SeatSummary[] = [{ ...base, id: 's1', name: 'PC-01', deviceId: 'd1' }, { ...base, id: 's2', name: 'PC-02', deviceId: 'd2' }];
    const { getByRole, getByText } = renderWorkspace({
      floorMap: { ...createFixtureFloorMapState(), seats, loadStatus: 'ready', source: 'backend' },
      session: { permissions: ['organization.devices.commands.dispatch'], organizationId: 'o' } as never,
      selectedSeatId: 's1'
    });
    const pick = getByRole('button', { name: t('op.map.pick.zone') });
    expect(pick.closest('.zone-group-head')).not.toBeNull();
    expect(pick.getAttribute('aria-pressed')).toBe('false');
    fireEvent.click(pick);
    expect(getByRole('button', { name: t('op.map.pick.zone') }).getAttribute('aria-pressed')).toBe('true');
    expect(getByText(t('op.map.bulk.count', { count: 2 }))).not.toBeNull();
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

  // Техник перезагружает ряд: Ctrl-клик собирает места, полоса отправляет одну команду всем,
  // окно заранее говорит, кому она не уйдёт.
  it('ctrl-click picks seats, and one command reaches every free PC among them', async () => {
    const base = { zone: 'Зал A', stateLabel: 'Свободно', player: 'Гость', remaining: 'Свободно', device: 'Device', command: 'Idle', app: 'Shell', isDeviceOnline: true };
    const seats: SeatSummary[] = [
      { ...base, id: 's1', name: 'PC-01', tone: 'ready', deviceId: 'd1' },
      { ...base, id: 's2', name: 'PC-02', tone: 'ready', deviceId: 'd2' },
      { ...base, id: 's3', name: 'PC-03', tone: 'active', activeSessionId: 'x', stateLabel: 'Занято', deviceId: 'd3' }
    ];
    const onPcControlAction = mock(async () => ({ detail: 'ok' }));
    const onSelectSeat = mock(() => {});
    const { getByRole, getByText, findByText } = renderWorkspace({
      floorMap: { ...createFixtureFloorMapState(), seats, loadStatus: 'ready', source: 'backend' },
      session: { permissions: ['organization.devices.commands.dispatch'], organizationId: 'o' } as never,
      actionsEnabled: true,
      selectedSeatId: 's1',
      onSelectSeat,
      onPcControlAction
    });

    fireEvent.click(getByRole('button', { name: /PC-01/ }), { ctrlKey: true });
    fireEvent.click(getByRole('button', { name: /PC-03/ }), { shiftKey: true });
    expect(getByText(t('op.map.bulk.count', { count: 3 }))).not.toBeNull();

    fireEvent.click(getByRole('button', { name: t('op.pc.reboot') }));
    expect(await findByText(/PC-03/, { selector: 'li strong' })).not.toBeNull();
    fireEvent.click(getByRole('dialog').querySelector('.ui-btn--danger')!);

    await waitFor(() => expect(onPcControlAction).toHaveBeenCalledTimes(2));
    expect(onPcControlAction.mock.calls.map((call) => (call as unknown[])[0] as SeatSummary).map((seat) => seat.id).sort()).toEqual(['s1', 's2']);
    // Выбор для команды не запускает сессию на свободном месте и не меняет карточку.
    expect(onSelectSeat).not.toHaveBeenCalledWith('s2');
  });
});
