import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import type { NetworkPeopleApi } from '@/api/platformClients/people';
import type { NetworkPerson } from '@/api/types';
import { PeopleScreen } from './PeopleScreen';

type Client = Pick<NetworkPeopleApi, 'lookupPerson' | 'banPerson' | 'liftBan'>;

function person(overrides: Partial<NetworkPerson> = {}): NetworkPerson {
  return {
    platformPersonId: '11111111-1111-1111-1111-111111111111',
    phoneNumber: '+992900000801',
    displayName: 'Фаррух',
    registeredAtUtc: '2026-06-01T10:00:00Z',
    networkBanAtUtc: null,
    networkBanReason: null,
    ...overrides
  };
}

function makeClient(overrides: Partial<Client> = {}): Client {
  return {
    lookupPerson: mock(async () => person()),
    banPerson: mock(async () => person({ networkBanAtUtc: '2026-08-23T10:00:00Z', networkBanReason: 'Подобрал чужой кошелёк' })),
    liftBan: mock(async () => person()),
    ...overrides
  };
}

function renderScreen(client: Client) {
  return render(
    <I18nProvider>
      <ToastProvider>
        <PeopleScreen client={client} />
      </ToastProvider>
    </I18nProvider>
  );
}

async function findAsync(user: ReturnType<typeof userEvent.setup>, phone = '+992900000801') {
  await user.type(screen.getByLabelText('Номер телефона'), phone);
  await user.click(screen.getByRole('button', { name: 'Найти' }));
}

describe('PeopleScreen', () => {
  it('находит человека по точному номеру', async () => {
    const client = makeClient();
    const user = userEvent.setup();
    renderScreen(client);

    await findAsync(user);

    await waitFor(() => expect(screen.getByText('+992900000801')).toBeTruthy());
    expect(screen.getByText('Фаррух')).toBeTruthy();
    expect(client.lookupPerson).toHaveBeenCalledWith('+992900000801');
  });

  // Незнакомый номер — это ответ, а не сбой: экран говорит его сам.
  it('говорит, что такого номера в сети нет', async () => {
    const client = makeClient({
      lookupPerson: mock(async () => { throw new PlatformApiError(404, 'not found'); })
    });
    const user = userEvent.setup();
    renderScreen(client);

    await findAsync(user);

    await waitFor(() => expect(screen.getByText('Такого номера в сети нет')).toBeTruthy());
  });

  // Запрет без причины некому объяснить и не на каком основании снять — кнопка ждёт причину.
  it('не даёт закрыть вход, пока не сказано, за что', async () => {
    const client = makeClient();
    const user = userEvent.setup();
    renderScreen(client);
    await findAsync(user);
    await waitFor(() => expect(screen.getByText('+992900000801')).toBeTruthy());

    await user.click(screen.getByRole('button', { name: 'Закрыть вход в сеть' }));
    const confirm = await screen.findByRole('button', { name: 'Закрыть вход' });
    expect(confirm.hasAttribute('disabled')).toBe(true);

    await user.type(screen.getByLabelText('За что'), 'Подобрал чужой кошелёк');
    await user.click(screen.getByRole('button', { name: 'Закрыть вход' }));

    await waitFor(() => expect(client.banPerson).toHaveBeenCalledWith(
      '11111111-1111-1111-1111-111111111111', 'Подобрал чужой кошелёк'));
  });

  it('показывает причину закрытого входа и открывает его обратно', async () => {
    const banned = person({ networkBanAtUtc: '2026-08-23T10:00:00Z', networkBanReason: 'Подобрал чужой кошелёк' });
    const client = makeClient({ lookupPerson: mock(async () => banned) });
    const user = userEvent.setup();
    renderScreen(client);
    await findAsync(user);

    await waitFor(() => expect(screen.getByText(/Подобрал чужой кошелёк/u)).toBeTruthy());

    await user.click(screen.getByRole('button', { name: 'Открыть вход' }));
    const confirm = await screen.findAllByRole('button', { name: 'Открыть вход' });
    await user.click(confirm[confirm.length - 1]);

    await waitFor(() => expect(client.liftBan).toHaveBeenCalledWith('11111111-1111-1111-1111-111111111111'));
  });

  // Обрывок номера — не поиск: панель платформы не место, где листают людей сети.
  it('объясняет, что по части номера здесь не ищут', async () => {
    const client = makeClient({
      lookupPerson: mock(async () => { throw new PlatformApiError(400, 'invalid_phone'); })
    });
    const user = userEvent.setup();
    renderScreen(client);

    await findAsync(user, '9000008');

    await waitFor(() => expect(
      screen.getByText('Номер набран не полностью. По части номера здесь не ищут.')).toBeTruthy());
  });
});
