import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { BookingRequestsLane } from './BookingRequestsLane';
import type { BookingItem } from './bookingModel';

afterEach(cleanup);

const NOW = Date.parse('2026-08-20T18:00:00Z');

function request(over: Partial<BookingItem> = {}): BookingItem {
  return {
    reservationId: 'r1', reservationGroupId: '', version: 1, state: 'pending', source: 'online',
    startMs: NOW + 3_600_000, endMs: NOW + 7_200_000, durationMinutes: 60,
    customerName: 'Камрон Р.', phoneNumber: '+992900000003', note: '', playerAccountId: '', platformPersonId: '',
    seatId: '', seatName: '', zoneName: '', tone: 'online', startedSessionId: '',
    respondByMs: NOW + 4 * 60_000 + 12_000,
    ...over
  };
}

const renderLane = (requests: BookingItem[]) =>
  render(
    <I18nProvider initialLocale="ru">
      <BookingRequestsLane
        requests={requests}
        busy={false}
        canManage
        onCreate={() => {}}
        onAccept={() => {}}
        onClarify={() => {}}
        nowProvider={() => NOW}
      />
    </I18nProvider>
  );

describe('BookingRequestsLane', () => {
  it('заявка носит свой срок с собой: «ответить за 4:12»', () => {
    renderLane([request()]);
    expect(screen.getByText('Ответить за 4:12')).toBeInTheDocument();
  });

  it('последняя минута обещания меняет тон карточки', () => {
    const { container } = renderLane([request({ respondByMs: NOW + 45_000 })]);
    expect(container.querySelector('.booking-lane-card')).toHaveClass('is-urgent');
    expect(screen.getByText('Ответить за 0:45')).toBeInTheDocument();
  });

  it('просроченная заявка не притворяется живой', () => {
    const { container } = renderLane([request({ respondByMs: NOW - 5_000 })]);
    expect(container.querySelector('.booking-lane-card')).toHaveClass('is-overdue');
    expect(screen.getByText('Срок ответа истёк')).toBeInTheDocument();
  });

  it('заявка без срока живёт как раньше — без пустой строки отсчёта', () => {
    const { container } = renderLane([request({ respondByMs: null })]);
    expect(container.querySelector('.booking-lane-respond')).toBeNull();
    expect(screen.getByText('Камрон Р.')).toBeInTheDocument();
  });
});
