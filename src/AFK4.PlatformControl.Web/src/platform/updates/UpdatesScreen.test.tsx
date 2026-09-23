import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import { UpdatesScreen } from './UpdatesScreen';

afterEach(cleanup);

function rolloutRow(state: string) {
  return {
    updateRolloutId: 'r1', updatePackageId: 'p1', component: 'organization-admin', version: '1.4.0',
    channel: 'stable', state, targetKind: 'organization', organizationIds: ['org-1'], branchIds: [],
    deviceIds: [], batchPercent: 100, reason: 'Публикация', createdByPlatformAdminUserId: 'a1',
    createdAtUtc: '2026-07-29T11:00:00Z', startsAtUtc: '2026-07-29T11:00:00Z', completedAtUtc: null
  };
}

const packageRow = {
  updatePackageId: 'p1', component: 'organization-admin', version: '1.4.0', channel: 'stable',
  artifactUri: 'https://updates.afk4.net/admin.msi', sha256: 'a'.repeat(64), signature: 'sig',
  signatureAlgorithm: 'ecdsa-p256-sha256-ieee-p1363', sizeBytes: 1024, state: 'registered',
  releaseNotes: 'Safe release.', createdByPlatformAdminUserId: 'a1', createdAtUtc: '2026-07-29T10:00:00Z',
  validatedByPlatformAdminUserId: null, validatedAtUtc: null, retiredAtUtc: null
};

function setup(state: string = 'registered', rollouts: unknown[] = [], listRollouts = mock().mockResolvedValue(rollouts)) {
  const updates = {
    listPackages: mock().mockResolvedValue([{ ...packageRow, state }]),
    listRollouts,
    registerPackage: mock(),
    changePackageState: mock().mockResolvedValue({ ...packageRow, state: 'validated' }),
    createRollout: mock().mockResolvedValue({}),
    changeRolloutState: mock().mockResolvedValue(rolloutRow('paused'))
  };
  const organizations = {
    listOrganizations: mock().mockResolvedValue([
      { organizationId: 'org-1' },
      { organizationId: 'org-2' }
    ])
  };
  render(
    <I18nProvider><ToastProvider>
      <UpdatesScreen client={updates as never} organizationsClient={organizations as never} />
    </ToastProvider></I18nProvider>
  );
  return { updates, organizations };
}

describe('UpdatesScreen', () => {
  it('shows the global package catalog and validates a registered package with a reason', async () => {
    const { updates } = setup();
    await screen.findByText('Панель AFK4.net');
    fireEvent.click(screen.getByRole('button', { name: 'Проверить пакет' }));
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'Подпись и хеш проверены.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить проверку' }));
    await waitFor(() => expect(updates.changePackageState).toHaveBeenCalledWith('p1', 'validated', 'Подпись и хеш проверены.'));
  });

  // Публикация обязана дойти до КАЖДОГО клиента: если этот тест ослабить до «вызвали
  // createRollout», обратно вернётся поэтапная выкатка на часть парка, а половина клубов
  // молча останется на старой версии.
  it('publishes a validated package to every organization at once', async () => {
    const { updates } = setup('validated');
    await screen.findByText('Панель AFK4.net');
    fireEvent.click(screen.getByRole('button', { name: 'Опубликовать' }));
    fireEvent.click(screen.getAllByRole('button', { name: 'Опубликовать' })[1]);
    await waitFor(() => expect(updates.createRollout).toHaveBeenCalled());
    const request = updates.createRollout.mock.calls[0][0];
    expect(request.targetKind).toBe('organization');
    expect(request.organizationIds).toEqual(['org-1', 'org-2']);
    expect(request.batchPercent).toBe(100);
  });

  // Остановка и пометка к откату существовали на сервере и не вызывались ниоткуда: плохая сборка
  // уезжала на весь парк, и прекратить раздачу было нечем.
  it('stops a rollout that is already going out, with a reason for the journal', async () => {
    const { updates } = setup('validated', [rolloutRow('active')]);
    await screen.findByText('Раскатывается');

    fireEvent.click(screen.getByRole('button', { name: 'Остановить' }));
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'Клубы сообщают о падениях' } });
    fireEvent.click(screen.getByRole('button', { name: 'Остановить раскатку' }));

    await waitFor(() => expect(updates.changeRolloutState)
      .toHaveBeenCalledWith('r1', 'paused', 'Клубы сообщают о падениях'));
  });

  it('resumes a stopped rollout and can still flag it for rollback', async () => {
    const { updates } = setup('validated', [rolloutRow('paused')]);
    await screen.findByText('Остановлена');

    fireEvent.click(screen.getByRole('button', { name: 'Пометить к откату' }));
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'Версия ломает кассу' } });
    // Подпись кнопки подтверждения намеренно отличается от подписи в строке: одинаковые подписи в
    // одном дереве означают, что ни тест, ни человек с клавиатуры не отличают «открыть» от «сделать».
    fireEvent.click(screen.getByRole('button', { name: 'Пометить раскатку к откату' }));

    await waitFor(() => expect(updates.changeRolloutState)
      .toHaveBeenCalledWith('r1', 'rollback-requested', 'Версия ломает кассу'));
    expect(screen.getByRole('button', { name: 'Возобновить' })).toBeInTheDocument();
  });

  // Сервер отвечает отказом на попытку изменить завершённую раскатку, поэтому кнопки, которая
  // обещает это сделать, быть не должно.
  it('offers nothing for a rollout that is already finished', async () => {
    setup('validated', [rolloutRow('rolled-back')]);
    await screen.findByText('Откачена');

    expect(screen.queryByRole('button', { name: 'Остановить' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Возобновить' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Пометить к откату' })).toBeNull();
  });

  // Причина обязательна на сервере: без неё запрос вернётся отказом, а человек не поймёт почему.
  it('will not send a state change without a reason', async () => {
    const { updates } = setup('validated', [rolloutRow('active')]);
    await screen.findByText('Раскатывается');

    fireEvent.click(screen.getByRole('button', { name: 'Остановить' }));
    fireEvent.click(screen.getByRole('button', { name: 'Остановить раскатку' }));

    expect(updates.changeRolloutState).not.toHaveBeenCalled();
  });

  it('offers no staged rollout controls', async () => {
    setup('validated');
    await screen.findByText('Панель AFK4.net');
    expect(screen.queryByRole('button', { name: 'Запустить rollout' })).toBeNull();
    expect(screen.queryByLabelText('Размер партии, %')).toBeNull();
  });

  // Форма отправляла имена приложений, которых сервер не знает («organization_admin»), и
  // регистрация пакета падала целиком — каждый раз, на любом приложении.
  it('регистрирует пакет под тем именем приложения, которое принимает сервер', async () => {
    const { updates } = setup();
    await screen.findByText('Панель AFK4.net');
    fireEvent.click(screen.getAllByRole('button', { name: 'Зарегистрировать пакет' })[0]!);

    fireEvent.change(screen.getByLabelText('Версия'), { target: { value: '1.5.0' } });
    fireEvent.change(screen.getByLabelText('Размер файла, байт'), { target: { value: '2048' } });
    fireEvent.change(screen.getByLabelText('URL installer'), { target: { value: 'https://updates.afk4.net/admin.msi' } });
    fireEvent.change(screen.getByLabelText('Отпечаток SHA-256'), { target: { value: 'b'.repeat(64) } });
    fireEvent.change(screen.getByLabelText('Подпись'), { target: { value: 'sig' } });
    fireEvent.change(screen.getByLabelText('Описание релиза'), { target: { value: 'Ничего страшного.' } });
    fireEvent.submit(screen.getByLabelText('Версия').closest('form')!);

    await waitFor(() => expect(updates.registerPackage).toHaveBeenCalled());
    expect(updates.registerPackage.mock.calls[0][0].component).toBe('organization-admin');
  });

  // Раскатки и каталог пакетов — разные запросы. Отказ раскаток не должен стирать каталог, но и
  // «Опубликовать» без них показывать нельзя: по раскаткам экран понимает, что сборка уже
  // опубликована, и без них предложил бы выложить её второй раз.
  it('отказ раскаток оставляет каталог, прячет публикацию и повторяет только раскатки', async () => {
    const { updates } = setup('validated', [], mock()
      .mockRejectedValueOnce(new PlatformApiError(500, 'boom'))
      .mockResolvedValue([]));

    expect(await screen.findByText('Панель AFK4.net')).toBeInTheDocument();
    expect(await screen.findByText('Не удалось загрузить раскатки — пока их нет, публиковать и менять раскатки нельзя')).toBeInTheDocument();
    expect(screen.getByText('Сервер платформы вернул ошибку. Повторите позже.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Опубликовать' })).toBeNull();
    const packageCalls = updates.listPackages.mock.calls.length;

    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('button', { name: 'Опубликовать' })).toBeInTheDocument();
    expect(updates.listPackages.mock.calls.length).toBe(packageCalls);
    expect(updates.listRollouts).toHaveBeenCalledTimes(2);
  });
});
