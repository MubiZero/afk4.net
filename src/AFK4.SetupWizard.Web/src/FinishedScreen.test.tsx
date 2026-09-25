import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { FinishedScreen } from './FinishedScreen';
import { HostBridgeRequestError, HostBridgeUnavailableError } from './hostBridge';
import type { WizardEnrollResult, WizardRole, WizardShellOutcome } from './wizardApi';

function enrolled(role: WizardRole, shell: WizardShellOutcome): WizardEnrollResult {
  return {
    organizationId: 'org-1',
    branchId: 'branch-1',
    deviceId: 'device-1',
    role,
    displayName: 'AFK4-PC-12',
    machineName: 'AFK4-PC-12',
    enrollmentState: 'approved',
    apiBaseUrl: 'https://api.afk4.net',
    updateChannel: 'stable',
    shell,
  };
}

const failedShell: WizardShellOutcome = { status: 'failed', exitCode: 1603, message: null };

const installed: WizardShellOutcome = { status: 'installed', exitCode: 0, message: null };

const agentStartFailed: WizardShellOutcome = {
  status: 'agent_start_failed',
  exitCode: 0,
  message: 'sc.exe exited with code 1053.',
};

function renderFinished(
  role: WizardRole = 'gaming_pc',
  shell: WizardShellOutcome = failedShell,
  provisionShell = mock(async (_role: WizardRole) => installed),
  reboot = mock(async () => {}),
) {
  const onClose = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <FinishedScreen
        result={enrolled(role, shell)}
        branchName="Главный зал"
        selectedSeat={null}
        stepNumber={5}
        provisionShell={provisionShell}
        reboot={reboot}
        onClose={onClose}
      />
    </I18nProvider>,
  );
  return { provisionShell, onClose, reboot };
}

describe('FinishedScreen', () => {
  afterEach(cleanup);

  // Без перезагрузки киоск не заработает — это единственная удача, о которой экран говорит вслух.
  it('после киоска просит перезагрузить ПК и перезагружает по кнопке', async () => {
    const { reboot } = renderFinished('gaming_pc', { ...installed, kiosk: { status: 'ready', message: null } });

    expect(screen.getByText(/Перезагрузите ПК — Windows войдёт в неё сама/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Перезагрузить сейчас' }));

    await waitFor(() => expect(reboot).toHaveBeenCalledTimes(1));
  });

  it('киоск не встал — говорит, что ПК работает без него, и даёт повторить', async () => {
    const provisionShell = mock(async (_role: WizardRole) => ({ ...installed, kiosk: { status: 'ready' as const, message: null } }));
    renderFinished('gaming_pc', { ...installed, kiosk: { status: 'failed', message: 'Access is denied.' } }, provisionShell);

    expect(screen.getByText(/Не получилось настроить автовход в учётку игрока/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить установку' }));

    expect(await screen.findByRole('button', { name: 'Перезагрузить сейчас' })).toBeInTheDocument();
  });

  it('без киоска строки киоска нет', () => {
    renderFinished('manager_workstation', installed);

    expect(screen.queryByRole('button', { name: 'Перезагрузить сейчас' })).toBeNull();
  });

  it('показывает итог установки: филиал, роль и имя ПК', () => {
    renderFinished('gaming_pc', installed);

    expect(screen.getByText('Главный зал')).toBeInTheDocument();
    expect(screen.getByText('AFK4-PC-12')).toBeInTheDocument();
  });

  // Зелёная плашка «всё хорошо» только шумит: строка появляется, когда есть что чинить.
  it('удачную установку отдельной строкой не празднует', () => {
    renderFinished('gaming_pc', installed);

    expect(screen.queryByRole('button', { name: 'Повторить установку' })).toBeNull();
  });

  // На рабочем месте управляющего ставится панель, а не оболочка игрока: одна строка на обе роли
  // обещала управляющему то, чего у него не будет.
  // И называет её тем же именем, что строка ниже: «Устанавливаем Organization Admin» и тут же
  // «Откройте панель клуба» — два имени одного места на одном экране. Теперь оба — Панель AFK4.net.
  it('называет то приложение, которое ставится этой роли', () => {
    renderFinished('manager_workstation');

    expect(screen.getByText('Не удалось установить Панель AFK4.net.')).toBeInTheDocument();
  });

  // Имя стоит в фразе, а не в списке компонентов: «установить Оболочка игрока» было списком.
  it('на игровом ПК называет оболочку игрока', () => {
    renderFinished('gaming_pc');

    expect(screen.getByText('Не удалось установить оболочку игрока.')).toBeInTheDocument();
  });

  it('удачный повтор убирает строку с ошибкой', async () => {
    const { provisionShell } = renderFinished('gaming_pc');

    fireEvent.click(screen.getByRole('button', { name: 'Повторить установку' }));

    await waitFor(() => expect(screen.queryByRole('button', { name: 'Повторить установку' })).toBeNull());
    expect(provisionShell.mock.calls[0]![0]).toBe('gaming_pc');
  });

  // Ради этого тест и написан: без catch кнопка молча возвращалась в исходное состояние, и
  // человек у ПК видел ровно то же, что до нажатия, — будто она не работает.
  it('сорвавшийся повтор называет причину, а не молчит', async () => {
    renderFinished('gaming_pc', failedShell, mock(async (_role: WizardRole) => {
      throw new HostBridgeRequestError('msiexec 1603', 'wizard_shell_provision_failed', null);
    }));

    fireEvent.click(screen.getByRole('button', { name: 'Повторить установку' }));

    expect(await screen.findByText('Повтор не удался.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Повторить установку' })).toBeEnabled();
  });

  // Место без связи с хостом — не «повтор не удался», а «экран мастера потерял связь с программой».
  it('обрыв моста называет своими словами', async () => {
    renderFinished('gaming_pc', failedShell, mock(async (_role: WizardRole) => {
      throw new HostBridgeUnavailableError();
    }));

    fireEvent.click(screen.getByRole('button', { name: 'Повторить установку' }));

    expect(await screen.findByText(/Экран мастера потерял связь с программой/)).toBeInTheDocument();
  });

  // Не запустившаяся служба — не то же, что не установившееся приложение: приложение как раз
  // встало. Одна фраза на два случая отправляла разбираться не туда.
  it('не запустившуюся службу называет службой, а не установкой приложения', () => {
    renderFinished('gaming_pc', agentStartFailed);

    expect(screen.getByText(/служба AFK4 не запустилась/)).toBeInTheDocument();
    expect(screen.queryByText(/Не удалось установить/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Повторить установку' })).toBeInTheDocument();
  });

  it('кнопка закрытия закрывает мастер', () => {
    const { onClose } = renderFinished('gaming_pc', installed);

    fireEvent.click(screen.getByRole('button', { name: 'Завершить' }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
