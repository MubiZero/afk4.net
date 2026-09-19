import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { afterEach, it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ClubsScreen } from './ClubsScreen';
import type { PulseOrganization } from '@/api/types';
import type { PulseApi } from '@/api/platformClients/pulse';
import { PlatformApiError } from '@/api/platformApi';

afterEach(() => localStorage.removeItem('afk4.locale'));

function org(over: Partial<PulseOrganization>): PulseOrganization {
  return {
    organizationId: 'o1',
    name: 'Cyber Zone',
    status: 'active',
    planCode: 'starter',
    subscriptionStatus: 'active',
    alertLevel: 'normal',
    outstandingMinorUnits: 0,
    currencyCode: 'TJS',
    alerts: [],
    clubs: [],
    ...over
  };
}

function client(overrides: Partial<Pick<PulseApi, 'getPulse'>> = {}) {
  return {
    pulse: {
      getPulse: mock().mockResolvedValue({ generatedAtUtc: '2026-08-03T00:00:00Z', organizations: [] }),
      ...overrides
    }
  } as never;
}

function setup(props: Partial<Parameters<typeof ClubsScreen>[0]> = {}) {
  const onViewChange = mock();
  const onOpenOrganization = mock();
  return render(
    <I18nProvider>
      <ClubsScreen client={client()} view="now" onViewChange={onViewChange} onOpenOrganization={onOpenOrganization} {...props} />
    </I18nProvider>
  );
}

it('renders the loudest network first in the "now" view', async () => {
  const getPulse = mock().mockResolvedValue({
    generatedAtUtc: '2026-08-03T00:00:00Z',
    organizations: [
      org({ organizationId: 'quiet', name: 'Arena', alertLevel: 'normal' }),
      org({ organizationId: 'loud', name: 'Zulu Zone', alertLevel: 'critical', alerts: [{ kind: 'agent_silent', level: 'critical', detailValue: null }] })
    ]
  });
  setup({ client: client({ getPulse }) });

  await waitFor(() => expect(screen.getAllByTestId('pulse-row')).toHaveLength(2));
  const rows = screen.getAllByTestId('pulse-row');
  expect(rows[0]).toHaveTextContent('Zulu Zone');
  expect(rows[1]).toHaveTextContent('Arena');
});

it('reports view switches to the URL owner', async () => {
  const getPulse = mock().mockResolvedValue({
    generatedAtUtc: '2026-08-03T00:00:00Z',
    organizations: [org({ organizationId: 'o1', name: 'Arena' })]
  });
  const onViewChange = mock();
  setup({ client: client({ getPulse }), onViewChange });

  await waitFor(() => expect(screen.getByText('Arena')).toBeInTheDocument());
  fireEvent.click(screen.getByRole('tab', { name: 'Все' }));
  expect(onViewChange).toHaveBeenCalledWith('all');
});

it('shows an error state, not an empty state, when the pulse fetch fails', async () => {
  const getPulse = mock().mockRejectedValue(new Error('network down'));
  setup({ client: client({ getPulse }) });

  await waitFor(() => expect(screen.getByRole('button', { name: 'Повторить' })).toBeInTheDocument());
  expect(screen.queryByTestId('pulse-row')).not.toBeInTheDocument();
});

// Строка сети целиком ведёт в карточку клиента. Раньше сюда же был подмешан второй смысл:
// клик по имени уводил внутрь, а клик рядом раскрывал клубы — две реакции на одну мишень.
it('opens the client card when the network row is clicked', async () => {
  const getPulse = mock().mockResolvedValue({
    generatedAtUtc: '2026-08-03T00:00:00Z',
    organizations: [org({ organizationId: 'o1', name: 'Arena' })]
  });
  const onOpenOrganization = mock();
  setup({ client: client({ getPulse }), onOpenOrganization });

  const row = await screen.findByRole('button', { name: /^Arena/u });
  fireEvent.click(row);
  expect(onOpenOrganization).toHaveBeenCalledWith('o1');
});

// Раскрытие — отдельная мишень: шеврон не должен уводить с экрана.
it('expands clubs from the chevron without leaving the screen', async () => {
  const getPulse = mock().mockResolvedValue({
    generatedAtUtc: '2026-08-03T00:00:00Z',
    organizations: Array.from({ length: 6 }, (_, index) => org({ organizationId: `o${index}`, name: `Arena ${index}` }))
  });
  const onOpenOrganization = mock();
  setup({ client: client({ getPulse }), onOpenOrganization });

  const chevron = await screen.findByRole('button', { name: 'Показать клубы сети Arena 0' });
  expect(chevron).toHaveAttribute('aria-expanded', 'false');
  fireEvent.click(chevron);
  expect(chevron).toHaveAttribute('aria-expanded', 'true');
  expect(onOpenOrganization).not.toHaveBeenCalled();
});

// Завести клуб — главное, зачем в эту панель заходят в первый раз. Экран заведения работал, но
// кнопка к нему потерялась при переделке списка в пульс: попасть туда можно было только набрав
// адрес руками.
it('даёт завести клуб', () => {
  const onCreateOrganization = mock();
  setup({ onCreateOrganization });

  fireEvent.click(screen.getByRole('button', { name: 'Новый клуб' }));

  expect(onCreateOrganization).toHaveBeenCalledTimes(1);
});

// Кнопка, которая гарантированно ответит отказом, хуже её отсутствия: право на заведение
// проверяет сервер, и без него кнопки нет.
it('без права заводить клубы кнопки не показывает', () => {
  setup();

  expect(screen.queryByRole('button', { name: 'Новый клуб' })).toBeNull();
});

// Сотрудник без права на обзор должен прочитать, что дело в правах, а не жать «Повторить» до
// бесконечности: раньше и отказ в правах, и упавший сервер, и оборванная сеть выглядели одним
// «Не удалось загрузить данные».
it('отказ в правах на экране назван своими словами', async () => {
  render(
    <I18nProvider>
      <ClubsScreen
        client={{ pulse: { getPulse: mock().mockRejectedValue(new PlatformApiError(403, 'Forbidden')) } } as never}
        view="now"
        onViewChange={mock()}
        onOpenOrganization={mock()}
      />
    </I18nProvider>
  );

  expect(await screen.findByText('Недостаточно прав для этого действия.')).toBeTruthy();
});
