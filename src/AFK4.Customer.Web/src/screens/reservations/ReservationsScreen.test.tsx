import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '@/components/ui/toast';
import { ReservationsScreen } from './ReservationsScreen';
import type { PlayerApiClient } from '@/api/playerApi';
import { branchChoice } from '@/branch/branchChoice';
import type { BrandingHallDto } from '@/api/types';

// Зал уже записан у человека со счётом в клубе: витрину выбора он не видит.
const settled = (branchId: string | null) => branchChoice([], null, branchId);

const hall = (branchId: string, name: string, city: string): BrandingHallDto =>
  ({ branchId, name, city, address: null });

function renderScreen(
  api: PlayerApiClient,
  phoneVerified: boolean,
  branchId: string | null = null,
  branch = settled(branchId),
  onChooseBranch: (id: string) => void = () => {}
) {
  return render(
    <I18nProvider>
      <ToastProvider autoDismissMs={1000}>
        <ReservationsScreen
          api={api}
          phoneVerified={phoneVerified}
          branch={branch}
          onChooseBranch={onChooseBranch}
        />
      </ToastProvider>
    </I18nProvider>
  );
}

const rules = {
  branchId: 'b1', acceptanceMode: 'auto' as const, respondWithinMinutes: 15,
  prepaymentRequired: false, activeReservations: 0, maxActiveReservations: null,
  holdSeatAfterStartMinutes: 20
};

it('lists reservations with a localized state', async () => {
  const api = { getReservations: mock().mockResolvedValue([
    { reservationId: 'r1', seatId: 's1', seatName: 'PC-7', startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T12:00:00Z', state: 'pending', note: null }
  ]) } as unknown as PlayerApiClient;
  renderScreen(api, true);
  expect(await screen.findByText('PC-7')).toBeInTheDocument();
  expect(screen.getByText('Ожидает подтверждения')).toBeInTheDocument();
});

it('hides the create form behind the D8 gate when the phone is unverified', async () => {
  const api = { getReservations: mock().mockResolvedValue([]) } as unknown as PlayerApiClient;
  renderScreen(api, false);
  expect(await screen.findByText(/подтвердите свой номер/i)).toBeInTheDocument();
  expect(screen.queryByLabelText('Начало')).not.toBeInTheDocument();
});

it('cancels a reservation after confirmation', async () => {
  const api = {
    getReservations: mock()
      .mockResolvedValueOnce([{ reservationId: 'r1', seatId: null, seatName: null, startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T12:00:00Z', state: 'pending', note: null }])
      .mockResolvedValueOnce([]),
    cancelReservation: mock().mockResolvedValue({ reservationId: 'r1', state: 'cancelled' })
  } as unknown as PlayerApiClient;
  (globalThis as { confirm: () => boolean }).confirm = () => true;
  renderScreen(api, true);
  fireEvent.click(await screen.findByRole('button', { name: /отменить/i }));
  await waitFor(() => expect(api.cancelReservation).toHaveBeenCalledWith('r1'));
});

// Заявка, ждущая ответа, не висит вечно: у клуба есть срок, и человек имеет право видеть, сколько
// его осталось, — иначе ожидание неотличимо от зависшего приложения.
it('ведёт обратный отсчёт до ответа клуба', async () => {
  const respondBy = new Date(Date.now() + 12 * 60_000 + 30_000).toISOString();
  const api = { getReservations: mock().mockResolvedValue([
    { reservationId: 'r1', seatId: null, seatName: null, startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T12:00:00Z', state: 'pending', note: null, respondByUtc: respondBy }
  ]) } as unknown as PlayerApiClient;
  renderScreen(api, true);
  expect(await screen.findByText(/Клуб ответит: осталось 12:(2|3)\d/)).toBeInTheDocument();
  expect(screen.getByText(/деньги вернутся целиком/)).toBeInTheDocument();
});

// Отказ без причины — это исчезнувшая бронь: человек видит, что её нет, и не понимает почему.
it('называет причину отказа и говорит про деньги', async () => {
  const api = { getReservations: mock().mockResolvedValue([
    { reservationId: 'r1', seatId: null, seatName: null, startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T12:00:00Z', state: 'rejected', note: null, rejectReasonCode: 'no_seats', rejectReasonNote: 'Турнир на все машины' }
  ]) } as unknown as PlayerApiClient;
  renderScreen(api, true);
  expect(await screen.findByText('Клуб отказал')).toBeInTheDocument();
  expect(screen.getByText('Мест на это время не осталось')).toBeInTheDocument();
  expect(screen.getByText('Турнир на все машины')).toBeInTheDocument();
  expect(screen.getByText(/Деньги вернулись целиком/)).toBeInTheDocument();
});

it('говорит, что срок вышел, а не показывает нули', async () => {
  const api = { getReservations: mock().mockResolvedValue([
    { reservationId: 'r1', seatId: null, seatName: null, startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T12:00:00Z', state: 'pending', note: null, respondByUtc: '2000-01-01T00:00:00Z' }
  ]) } as unknown as PlayerApiClient;
  renderScreen(api, true);
  // Текст берётся из общего каталога: он один на приложение и на веб, и разъехаться им нельзя.
  expect(await screen.findByText(/Клуб не ответил вовремя/)).toBeInTheDocument();
  expect(screen.queryByText(/осталось/)).not.toBeInTheDocument();
});

// Подтверждённой брони отвечать больше не на что — отсчёт у неё был бы обещанием ни о чём.
it('не ведёт отсчёт у подтверждённой брони', async () => {
  const api = { getReservations: mock().mockResolvedValue([
    { reservationId: 'r1', seatId: null, seatName: null, startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T12:00:00Z', state: 'confirmed', note: null, respondByUtc: null }
  ]) } as unknown as PlayerApiClient;
  renderScreen(api, true);
  await screen.findByText('Подтверждена');
  expect(screen.queryByText(/Клуб ответит/)).not.toBeInTheDocument();
});

it('предупреждает, что заявку смотрит администратор, и называет срок', async () => {
  const api = {
    getReservations: mock().mockResolvedValue([]),
    getBookingRules: mock().mockResolvedValue({ ...rules, acceptanceMode: 'manual', respondWithinMinutes: 15 })
  } as unknown as PlayerApiClient;
  renderScreen(api, true, 'b1');
  expect(await screen.findByText(/Заявку смотрит администратор — ответит за 15 минут/)).toBeInTheDocument();
});

// Филиал вправе не принимать брони из приложения. Оставить форму значило бы вести человека к
// отказу, о котором известно заранее.
it('убирает форму, когда филиал брони не принимает', async () => {
  const api = {
    getReservations: mock().mockResolvedValue([]),
    getBookingRules: mock().mockResolvedValue({ ...rules, acceptanceMode: 'off' })
  } as unknown as PlayerApiClient;
  renderScreen(api, true, 'b1');
  expect(await screen.findByText(/не принимает брони из приложения/)).toBeInTheDocument();
  expect(screen.queryByLabelText('Начало')).not.toBeInTheDocument();
});

it('предупреждает о заморозке денег там, где клуб её требует', async () => {
  const api = {
    getReservations: mock().mockResolvedValue([]),
    getBookingRules: mock().mockResolvedValue({ ...rules, prepaymentRequired: true })
  } as unknown as PlayerApiClient;
  renderScreen(api, true, 'b1');
  expect(await screen.findByText(/придержим на кошельке до вашего прихода/)).toBeInTheDocument();
});

it('объясняет исчерпанный потолок заявок вместо молчаливого отказа', async () => {
  const api = {
    getReservations: mock().mockResolvedValue([]),
    getBookingRules: mock().mockResolvedValue({ ...rules, activeReservations: 1, maxActiveReservations: 1 })
  } as unknown as PlayerApiClient;
  renderScreen(api, true, 'b1');
  expect(await screen.findByText(/Больше заявок сейчас не принимаем/)).toBeInTheDocument();
});

// Правила — подсказка, а не разрешение: их отсутствие не должно запирать бронь, которую сервер
// принял бы.
it('не запирает бронь, если правила не загрузились', async () => {
  const api = {
    getReservations: mock().mockResolvedValue([]),
    getBookingRules: mock().mockRejectedValue(new Error('offline'))
  } as unknown as PlayerApiClient;
  renderScreen(api, true, 'b1');
  expect(await screen.findByLabelText('Начало')).toBeInTheDocument();
});

// Первое действие в сети из нескольких залов упирается в вопрос «в какой вы придёте»: счёт
// человеку открывает эта самая бронь, и гадать зал за него сервер не станет.
it('у сети без счёта спрашивает зал и не даёт бронировать, пока он не назван', async () => {
  const api = {
    getReservations: mock().mockResolvedValue([]),
    getBookingRules: mock().mockResolvedValue(rules),
    createReservation: mock().mockResolvedValue({})
  } as unknown as PlayerApiClient;
  const halls = [hall('b1', 'На Рудаки', 'Душанбе'), hall('b2', 'В Худжанде', 'Худжанд')];

  renderScreen(api, true, null, branchChoice(halls, null, null));

  expect(await screen.findByText('В какой зал вы придёте?')).toBeInTheDocument();
  fireEvent.change(await screen.findByLabelText('Начало'), { target: { value: '2999-01-01T10:00' } });
  fireEvent.change(screen.getByLabelText('Конец'), { target: { value: '2999-01-01T12:00' } });
  fireEvent.click(screen.getByRole('button', { name: /забронировать/i }));

  expect(await screen.findByText('Сначала выберите зал, в который придёте.')).toBeInTheDocument();
  expect(api.createReservation).not.toHaveBeenCalled();
});

it('названный зал уезжает вместе с бронью', async () => {
  const api = {
    getReservations: mock().mockResolvedValue([]),
    getBookingRules: mock().mockResolvedValue(rules),
    createReservation: mock().mockResolvedValue({})
  } as unknown as PlayerApiClient;
  const halls = [hall('b1', 'На Рудаки', 'Душанбе'), hall('b2', 'В Худжанде', 'Худжанд')];

  renderScreen(api, true, null, branchChoice(halls, 'b2', null));

  fireEvent.change(await screen.findByLabelText('Начало'), { target: { value: '2999-01-01T10:00' } });
  fireEvent.change(screen.getByLabelText('Конец'), { target: { value: '2999-01-01T12:00' } });
  fireEvent.click(screen.getByRole('button', { name: /забронировать/i }));

  await waitFor(() => expect(api.createReservation).toHaveBeenCalled());
  const sent = (api.createReservation as unknown as ReturnType<typeof mock>).mock.calls[0][0];
  expect(sent.branchId).toBe('b2');
});

// Единственный зал сети — не выбор, а данность: вопрос над ним был бы вопросом без вопроса.
it('у сети с одним залом ничего не спрашивает, но зал называет', async () => {
  const api = {
    getReservations: mock().mockResolvedValue([]),
    getBookingRules: mock().mockResolvedValue(rules),
    createReservation: mock().mockResolvedValue({})
  } as unknown as PlayerApiClient;

  renderScreen(api, true, null, branchChoice([hall('b1', 'На Рудаки', 'Душанбе')], null, null));

  expect(screen.queryByText('В какой зал вы придёте?')).not.toBeInTheDocument();
  fireEvent.change(await screen.findByLabelText('Начало'), { target: { value: '2999-01-01T10:00' } });
  fireEvent.change(screen.getByLabelText('Конец'), { target: { value: '2999-01-01T12:00' } });
  fireEvent.click(screen.getByRole('button', { name: /забронировать/i }));

  await waitFor(() => expect(api.createReservation).toHaveBeenCalled());
  const sent = (api.createReservation as unknown as ReturnType<typeof mock>).mock.calls[0][0];
  expect(sent.branchId).toBe('b1');
});

// Отказ сервера про зал не должен выглядеть как «время занято»: человек ищет другой час, а
// лечится это выбором зала.
it('отказ branch_required объясняется словами про зал', async () => {
  const api = {
    getReservations: mock().mockResolvedValue([]),
    getBookingRules: mock().mockResolvedValue(rules),
    createReservation: mock().mockRejectedValue(Object.assign(new Error('branch_required'), { status: 409 }))
  } as unknown as PlayerApiClient;

  renderScreen(api, true, null, branchChoice([hall('b1', 'На Рудаки', 'Душанбе')], null, null));

  fireEvent.change(await screen.findByLabelText('Начало'), { target: { value: '2999-01-01T10:00' } });
  fireEvent.change(screen.getByLabelText('Конец'), { target: { value: '2999-01-01T12:00' } });
  fireEvent.click(screen.getByRole('button', { name: /забронировать/i }));

  expect(await screen.findByText('Сначала выберите зал, в который придёте.')).toBeInTheDocument();
});
