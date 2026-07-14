import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { OperatorFloorMapState } from './floorMapState';
import type { SeatSummary } from './operatorData';
import { ToastProvider } from './operatorToast';
import { BackendBookingWorkspace } from './BackendBookingWorkspace';

afterEach(cleanup);

function seat(id: string, name: string): SeatSummary {
  return {
    id, zone: 'Зал A', name, tone: 'ready', stateLabel: 'Свободен', player: '', remaining: '',
    device: name, command: '', app: '', activeSessionId: null
  };
}

const floorMap: OperatorFloorMapState = {
  branchId: 'branch-1', branchName: 'Тестовый клуб', seats: [seat('a', 'PC-01'), seat('b', 'PC-02')],
  zones: [], walls: [], etag: null, source: 'backend', loadStatus: 'ready', error: null,
  isOffline: false, cachedAtMs: null
};

describe('BackendBookingWorkspace modifier draft transitions', () => {
  it('после закрытия старой формы Ctrl-click начинает чистый draft, а повторный click снимает место', async () => {
    const result = render(
      <I18nProvider>
        <ToastProvider>
          <BackendBookingWorkspace floorMap={floorMap} backend={null} currencyCode="TJS" onOpenSeat={() => {}} />
        </ToastProvider>
      </I18nProvider>
    );

    await waitFor(() => expect(result.container.querySelectorAll('.booking-row-track')).toHaveLength(2));
    const tracks = result.container.querySelectorAll<HTMLElement>('.booking-row-track');
    for (const track of tracks) {
      track.getBoundingClientRect = () => ({ left: 0, top: 0, width: 2400, height: 38, right: 2400, bottom: 38, x: 0, y: 0, toJSON: () => ({}) }) as DOMRect;
    }

    // Старый закрытый draft A@10:00 с заполненным гостевым контактом.
    fireEvent.click(tracks[0], { clientX: 1000 });
    const oldDrawer = await screen.findByRole('dialog', { name: 'Новая бронь' });
    fireEvent.change(within(oldDrawer).getByRole('combobox', { name: 'Поиск клиентов' }), { target: { value: 'Старый клиент' } });
    fireEvent.change(within(oldDrawer).getByRole('textbox', { name: 'Телефон' }), { target: { value: '93 111 22 33' } });
    fireEvent.click(within(oldDrawer).getByRole('button', { name: 'Отмена' }));

    // Ctrl-click B@15:00 вне create должен начать заново, не продолжить закрытый draft.
    fireEvent.click(tracks[1], { clientX: 1500, ctrlKey: true });
    const freshDrawer = await screen.findByRole('dialog', { name: 'Новая бронь' });
    const chips = freshDrawer.querySelectorAll('.booking-seat-chip');
    expect(chips).toHaveLength(1);
    expect(chips[0]).toHaveTextContent('PC-02');
    expect(freshDrawer).not.toHaveTextContent('PC-01');
    expect(within(freshDrawer).getByRole<HTMLInputElement>('combobox', { name: 'Поиск клиентов' }).value).toBe('');
    expect(within(freshDrawer).getByRole<HTMLInputElement>('textbox', { name: 'Телефон' }).value).toBe('');
    expect(freshDrawer).toHaveTextContent(/15:00–16:00/);

    // В уже активном create drawer тот же modifier-click — именно toggle-off.
    fireEvent.click(tracks[1], { clientX: 1500, metaKey: true });
    expect(freshDrawer.querySelector('.booking-seat-chip')).toBeNull();
    expect(within(freshDrawer).getByRole('button', { name: 'Создать бронь' })).toBeDisabled();
  });
});
