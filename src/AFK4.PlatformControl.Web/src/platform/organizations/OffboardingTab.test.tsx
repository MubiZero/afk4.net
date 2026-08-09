import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import type { OrganizationOffboarding } from '@/api/types';
import type { OffboardingApi } from '@/api/platformClients/offboarding';
import { OffboardingTab } from './OffboardingTab';

type Client = Pick<OffboardingApi, 'getOffboarding' | 'purge' | 'downloadExport'>;

const ORGANIZATION_ID = '11111111-1111-1111-1111-111111111111';

function state(overrides: Partial<OrganizationOffboarding> = {}): OrganizationOffboarding {
  return {
    organizationId: ORGANIZATION_ID,
    slug: 'leaving-club',
    status: 'deletion_pending',
    purgeEligibleAtUtc: '2026-08-01T00:00:00Z',
    purgedAtUtc: null,
    canPurge: true,
    ...overrides
  };
}

function makeClient(overrides: Partial<Client> = {}): Client {
  return {
    getOffboarding: mock(async () => state()),
    purge: mock(async () => ({ players: 1, staffUsers: 1, sessions: 1, sales: 1, devices: 1, branches: 1, clubAuditRecords: 1 })),
    downloadExport: mock(async () => new Blob(['zip'])),
    ...overrides
  };
}

function renderTab(client: Client) {
  return render(
    <I18nProvider>
      <ToastProvider>
        <OffboardingTab client={client} organizationId={ORGANIZATION_ID} />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('OffboardingTab', () => {
  it('стирание требует точного короткого имени', async () => {
    const client = makeClient();
    renderTab(client);
    await screen.findByText('leaving-club');

    await userEvent.click(screen.getByRole('button', { name: 'Стереть данные' }));
    const field = await screen.findByLabelText('Короткое имя клуба');

    // Пока имя не набрано, подтверждение недоступно: кнопка «да, я уверен» здесь недостаточна.
    const confirm = screen.getAllByRole('button', { name: 'Стереть данные' }).at(-1)!;
    expect(confirm).toBeDisabled();

    await userEvent.type(field, 'leaving-club');
    await userEvent.click(confirm);

    await waitFor(() => expect(client.purge).toHaveBeenCalledWith(ORGANIZATION_ID, 'leaving-club'));
  });

  it('до наступления срока стирание выключено и объясняет причину', async () => {
    const client = makeClient({
      getOffboarding: mock(async () => state({ canPurge: false }))
    });
    renderTab(client);
    await screen.findByText('leaving-club');

    expect(screen.getByRole('button', { name: 'Стереть данные' })).toBeDisabled();
    expect(screen.getByText(/Срок ещё не наступил/)).toBeInTheDocument();
  });

  it('у клуба вне заявки на уход стирание выключено', async () => {
    const client = makeClient({
      getOffboarding: mock(async () => state({ status: 'active', canPurge: false, purgeEligibleAtUtc: null }))
    });
    renderTab(client);
    await screen.findByText('leaving-club');

    expect(screen.getByRole('button', { name: 'Стереть данные' })).toBeDisabled();
    expect(screen.getByText(/только после заявки на уход/)).toBeInTheDocument();
  });

  it('у стёртого клуба не предлагает ни выгрузки, ни стирания', async () => {
    const client = makeClient({
      getOffboarding: mock(async () => state({
        status: 'purged',
        purgedAtUtc: '2026-08-09T00:00:00Z',
        purgeEligibleAtUtc: null,
        canPurge: false
      }))
    });
    renderTab(client);
    await screen.findByText('leaving-club');

    expect(screen.queryByRole('button', { name: 'Стереть данные' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Выгрузить данные' })).not.toBeInTheDocument();
    expect(screen.getByText(/Осталась архивная строка/)).toBeInTheDocument();
  });

  it('объясняет отказ сервера его собственными словами', async () => {
    const client = makeClient({
      purge: mock(async () => {
        throw new PlatformApiError(400, 'slug_mismatch', 'slug_mismatch', null, '');
      })
    });
    renderTab(client);
    await screen.findByText('leaving-club');

    await userEvent.click(screen.getByRole('button', { name: 'Стереть данные' }));
    await userEvent.type(await screen.findByLabelText('Короткое имя клуба'), 'wrong');
    await userEvent.click(screen.getAllByRole('button', { name: 'Стереть данные' }).at(-1)!);

    const toast = await screen.findByRole('status');
    expect(toast).toHaveTextContent('данные не тронуты');
  });
});
