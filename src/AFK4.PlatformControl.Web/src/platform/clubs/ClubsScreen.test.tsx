import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { afterEach, it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ClubsScreen } from './ClubsScreen';
import type { PulseOrganization } from '@/api/types';
import type { PulseApi } from '@/api/platformClients/pulse';

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
  return render(
    <I18nProvider>
      <ClubsScreen client={client()} view="now" onViewChange={onViewChange} {...props} />
    </I18nProvider>
  );
}

it('renders the loudest network first in the "now" view', async () => {
  const getPulse = mock().mockResolvedValue({
    generatedAtUtc: '2026-08-03T00:00:00Z',
    organizations: [
      org({ organizationId: 'quiet', name: 'Arena', alertLevel: 'normal' }),
      org({ organizationId: 'loud', name: 'Zulu Zone', alertLevel: 'critical', alerts: [{ kind: 'agent_silent', level: 'critical', detail: null }] })
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
