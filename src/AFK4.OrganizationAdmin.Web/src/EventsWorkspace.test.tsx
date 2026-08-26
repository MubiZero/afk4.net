import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { EventsWorkspace } from './EventsWorkspace';
import type {
  CreateTournamentRequest,
  TournamentDto,
  TournamentParticipantDto,
  UpdateTournamentRequest
} from './operatorApiClients';

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: [] },
  branchId: 'b1'
};

function event(overrides: Partial<TournamentDto> = {}): TournamentDto {
  return {
    tournamentId: 't1',
    branchId: 'b1',
    title: 'Ночь Counter-Strike',
    description: 'Пять на пять',
    discipline: 'Counter-Strike',
    startsAtUtc: '2026-08-28T14:00:00Z',
    entryFee: { currencyCode: 'TJS', minorUnits: 2000 },
    capacity: 10,
    state: 'draft',
    registeredCount: 0,
    createdAtUtc: '2026-08-26T10:00:00Z',
    updatedAtUtc: '2026-08-26T10:00:00Z',
    cancelledAtUtc: null,
    cancelReason: '',
    ...overrides
  };
}

function client(initial: TournamentDto[] = [], participants: TournamentParticipantDto[] = []) {
  const created: CreateTournamentRequest[] = [];
  const updated: UpdateTournamentRequest[] = [];
  const published: string[] = [];
  const cancelled: { id: string; reason: string }[] = [];
  let store = [...initial];
  return {
    created,
    updated,
    published,
    cancelled,
    list: async () => store,
    create: async (request: CreateTournamentRequest) => {
      created.push(request);
      const dto = event({ tournamentId: 'new', ...request, entryFee: { currencyCode: 'TJS', minorUnits: request.entryFeeMinorUnits } });
      store = [dto, ...store];
      return dto;
    },
    update: async (id: string, request: UpdateTournamentRequest) => {
      updated.push(request);
      return event({ tournamentId: id, ...request });
    },
    publish: async (id: string) => {
      published.push(id);
      store = store.map((item) => (item.tournamentId === id ? { ...item, state: 'published' } : item));
      return store.find((item) => item.tournamentId === id)!;
    },
    cancel: async (id: string, reason: string) => {
      cancelled.push({ id, reason });
      store = store.map((item) => (item.tournamentId === id ? { ...item, state: 'cancelled', cancelReason: reason } : item));
      return store.find((item) => item.tournamentId === id)!;
    },
    participants: async () => participants
  };
}

function renderWorkspace(c: ReturnType<typeof client>, canManage?: boolean) {
  render(
    <I18nProvider initialLocale="ru">
      <EventsWorkspace backend={backend as never} canManage={canManage} client={c as never} />
    </I18nProvider>
  );
}

describe('EventsWorkspace', () => {
  afterEach(() => cleanup());

  it('заводит событие через дровер', async () => {
    const c = client();
    renderWorkspace(c);
    fireEvent.click((await screen.findAllByRole('button', { name: 'Новое событие' }))[0]!);

    fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Ночь FIFA' } });
    fireEvent.change(screen.getByLabelText('Начало'), { target: { value: '2026-08-28T19:00' } });
    fireEvent.change(screen.getByLabelText('Взнос, с.'), { target: { value: '25' } });
    fireEvent.change(screen.getByLabelText('Мест'), { target: { value: '8' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(c.created.length).toBe(1));
    expect(c.created[0]!.title).toBe('Ночь FIFA');
    // Взнос вводят в сомони, а уезжает он в дирамах — считать нули за клуб не его работа.
    expect(c.created[0]!.entryFeeMinorUnits).toBe(2500);
    expect(c.created[0]!.capacity).toBe(8);
  });

  it('без названия не сохраняет и говорит почему', async () => {
    const c = client();
    renderWorkspace(c);
    fireEvent.click((await screen.findAllByRole('button', { name: 'Новое событие' }))[0]!);

    fireEvent.change(screen.getByLabelText('Начало'), { target: { value: '2026-08-28T19:00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Без названия событие не найдут');
    expect(c.created.length).toBe(0);
  });

  // Событие без даты — это не событие: игроку некуда собираться.
  it('без даты начала не сохраняет', async () => {
    const c = client();
    renderWorkspace(c);
    fireEvent.click((await screen.findAllByRole('button', { name: 'Новое событие' }))[0]!);

    fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Ночь FIFA' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Укажите, когда событие начнётся');
    expect(c.created.length).toBe(0);
  });

  it('черновик публикуется одной кнопкой', async () => {
    const c = client([event()]);
    renderWorkspace(c);
    fireEvent.click(await screen.findByText('Ночь Counter-Strike'));

    fireEvent.click(await screen.findByRole('button', { name: 'Опубликовать' }));

    await waitFor(() => expect(c.published).toEqual(['t1']));
  });

  // Опубликованное событие отменяют, а не публикуют второй раз.
  it('у опубликованного события кнопка отмены, а не публикации', async () => {
    const c = client([event({ state: 'published' })]);
    renderWorkspace(c);
    fireEvent.click(await screen.findByText('Ночь Counter-Strike'));

    expect(await screen.findByRole('button', { name: 'Отменить событие' })).toBeDefined();
    expect(screen.queryByRole('button', { name: 'Опубликовать' })).toBeNull();
  });

  // Отмена возвращает взносы — стойка должна прочитать это до нажатия, а игрок потом прочитает
  // причину вместо голого «отменено».
  it('отмена предупреждает о возврате и спрашивает причину', async () => {
    const c = client([event({ state: 'published', registeredCount: 3 })]);
    renderWorkspace(c);
    fireEvent.click(await screen.findByText('Ночь Counter-Strike'));
    fireEvent.click(await screen.findByRole('button', { name: 'Отменить событие' }));

    expect(await screen.findByText(/Взносы вернутся 3 записавшимся/)).toBeDefined();
    fireEvent.change(screen.getByLabelText('Причина — её прочитает игрок'), {
      target: { value: 'Свет отключили' }
    });
    fireEvent.click(screen.getAllByRole('button', { name: 'Отменить событие' }).at(-1)!);

    await waitFor(() => expect(c.cancelled).toEqual([{ id: 't1', reason: 'Свет отключили' }]));
  });

  it('показывает, кто записался, с номерами', async () => {
    const c = client([event({ state: 'published', registeredCount: 1 })], [
      {
        tournamentRegistrationId: 'r1',
        playerAccountId: 'p1',
        displayName: 'Фаррух',
        phoneNumber: '+992937380070',
        entryFeePaid: { currencyCode: 'TJS', minorUnits: 2000 },
        registeredAtUtc: '2026-08-26T11:00:00Z'
      }
    ]);
    renderWorkspace(c);
    fireEvent.click(await screen.findByText('Ночь Counter-Strike'));

    expect(await screen.findByText('Фаррух')).toBeDefined();
    expect(screen.getByText(/992937380070/)).toBeDefined();
  });

  // У события без потолка «3 из 0» было бы враньём.
  it('событие без ограничения мест показывает просто число записавшихся', async () => {
    const c = client([event({ capacity: 0, registeredCount: 12, state: 'published' })]);
    renderWorkspace(c);

    expect(await screen.findByText('12')).toBeDefined();
    expect(screen.queryByText(/из 0/)).toBeNull();
  });

  it('без права на события форма только читается', async () => {
    const c = client([event()]);
    renderWorkspace(c, false);
    fireEvent.click(await screen.findByText('Ночь Counter-Strike'));

    expect((screen.getByLabelText('Название') as HTMLInputElement).disabled).toBe(true);
    expect(screen.queryByRole('button', { name: 'Сохранить' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Опубликовать' })).toBeNull();
  });
});
