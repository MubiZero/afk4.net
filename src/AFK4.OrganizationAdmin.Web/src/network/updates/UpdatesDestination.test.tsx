import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import { UpdatesDestination } from './UpdatesDestination';
import type { UpdateStatusClient } from './useUpdateStatus';
import type { OrganizationAdminUpdatePreferenceDto, UpdateRolloutStatusDto } from '../../api/clients/updates';

const rollout: UpdateRolloutStatusDto = {
  updateRolloutId: 'rollout-1',
  organizationId: 'org-1',
  branchId: 'branch-1',
  updatePackageId: 'package-1',
  component: 'organization-admin',
  version: '2.4.0',
  channel: 'stable',
  state: 'active',
  startsAtUtc: '2026-08-01T00:00:00Z',
  deviceStatuses: [
    {
      deviceId: 'device-1',
      updateRolloutId: 'rollout-1',
      updatePackageId: 'package-1',
      component: 'organization-admin',
      installedVersion: '2.3.1',
      targetVersion: '2.4.0',
      status: 'downloaded',
      message: 'Пакет загружен, ждём удобного времени',
      updatedAtUtc: '2026-08-09T10:00:00Z'
    }
  ]
};

const preference: OrganizationAdminUpdatePreferenceDto = {
  organizationId: 'org-1',
  branchId: 'branch-1',
  maintenanceWindowStart: '03:00:00',
  maintenanceWindowEnd: '05:00:00',
  timeZone: 'Asia/Dushanbe'
};

function makeClient(overrides: Partial<UpdateStatusClient> = {}): UpdateStatusClient {
  return {
    getRolloutStatuses: mock(async () => [rollout]),
    getPreference: mock(async () => preference),
    updatePreference: mock(async (_branchId: string, request) => ({ ...preference, ...request })),
    ...overrides
  };
}

const backend = {
  config: { platformBaseUrl: 'http://test', currencyCode: 'TJS' },
  session: {
    accessToken: 'token',
    accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    organizationId: 'org-1',
    permissions: ['organization.updates.status.view', 'organization.branches.settings.manage']
  },
  branchId: 'branch-1'
};

// Направление «Обновления» гейтится правом на настройки филиала (networkNav), поэтому экран
// всегда открывается тому, кто вправе менять время — отдельного режима «только чтение» нет.
function renderUpdates(client: UpdateStatusClient) {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <UpdatesDestination backend={backend as never} client={client} />
      </ToastProvider>
    </I18nProvider>
  );
}

afterEach(() => cleanup());

describe('UpdatesDestination', () => {
  it('показывает версию, состояние и текущее окно обновления', async () => {
    renderUpdates(makeClient());

    expect(await screen.findByText('2.3.1')).toBeInTheDocument();
    expect(screen.getByText('2.4.0')).toBeInTheDocument();
    expect(screen.getByText('Загружено')).toBeInTheDocument();
    expect(screen.getByText('Asia/Dushanbe')).toBeInTheDocument();
    expect((screen.getByLabelText('С') as HTMLInputElement).value).toBe('03:00');
    expect((screen.getByLabelText('До') as HTMLInputElement).value).toBe('05:00');
  });

  it('сохраняет новое окно и шлёт время с секундами', async () => {
    const client = makeClient();
    renderUpdates(client);
    const from = await screen.findByLabelText('С');

    fireEvent.change(from, { target: { value: '02:00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить время' }));

    await waitFor(() => expect(client.updatePreference).toHaveBeenCalledWith('branch-1', {
      organizationId: 'org-1',
      maintenanceWindowStart: '02:00:00',
      maintenanceWindowEnd: '05:00:00'
    }));
  });

  // Сервер отвергает окно нулевой длины, поэтому такое даже не отправляем.
  it('не даёт сохранить окно с совпадающими краями', async () => {
    renderUpdates(makeClient());
    const from = await screen.findByLabelText('С');

    fireEvent.change(from, { target: { value: '05:00' } });

    expect(screen.getByRole('button', { name: 'Сохранить время' })).toBeDisabled();
    expect(screen.getByText('Начало и конец должны отличаться.')).toBeInTheDocument();
  });

  // В браузере нативного моста нет — кнопка «перезапустить» гарантированно откажет, поэтому
  // вместо неё объясняем, где действие доступно.
  it('вне приложения на ПК предлагает не кнопку, а объяснение', async () => {
    renderUpdates(makeClient());

    await screen.findByText('2.3.1');
    expect(screen.queryByRole('button', { name: 'Перезапустить и обновить' })).toBeNull();
    expect(screen.getByText(/Обновление ставится из программы на самом ПК/)).toBeInTheDocument();
  });

  it('когда раскатки нет, честно говорит что обновлений нет', async () => {
    renderUpdates(makeClient({ getRolloutStatuses: mock(async () => []) }));

    expect(await screen.findByText('Обновлений нет')).toBeInTheDocument();
    expect(screen.getByLabelText('С')).toBeInTheDocument();
  });

  it('на сбое загрузки показывает ошибку экрана, а не пустые поля', async () => {
    renderUpdates(makeClient({
      getPreference: mock(async () => { throw new Error('boom'); })
    }));

    expect(await screen.findByRole('button', { name: 'Повторить' })).toBeInTheDocument();
    expect(screen.queryByLabelText('С')).toBeNull();
  });

  // Раскатки клиентских ПК приходят в том же ответе — экран про рабочее место администратора
  // не должен показывать чужой компонент как свой.
  it('игнорирует раскатки других компонентов', async () => {
    renderUpdates(makeClient({
      getRolloutStatuses: mock(async () => [{ ...rollout, component: 'player-shell', version: '9.9.9' }])
    }));

    expect(await screen.findByText('Обновлений нет')).toBeInTheDocument();
    expect(screen.queryByText('9.9.9')).toBeNull();
  });
});
