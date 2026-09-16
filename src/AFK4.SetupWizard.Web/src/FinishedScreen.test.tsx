import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import type { WizardEnrollResult, WizardRole, WizardShellOutcome } from './wizardApi';

// bun's mock.module is not hoisted above static imports — register before importing the screen.
const provisionShell = mock(async (): Promise<WizardShellOutcome> => ({
  status: 'installed',
  exitCode: 0,
  message: null,
}));
const closeWizard = mock(() => {});

const actualApi = await import('./wizardApi');
mock.module('./wizardApi', () => ({ ...actualApi, provisionShell, closeWizard }));

const { FinishedScreen } = await import('./FinishedScreen');
const { HostBridgeRequestError, HostBridgeUnavailableError } = await import('./hostBridge');

afterAll(() => {
  mock.module('./wizardApi', () => actualApi);
});

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

function renderFinished(role: WizardRole = 'gaming_pc', shell: WizardShellOutcome = failedShell) {
  render(
    <I18nProvider initialLocale="ru">
      <FinishedScreen result={enrolled(role, shell)} branchName="Главный зал" selectedSeat={null} stepNumber={5} />
    </I18nProvider>,
  );
}

describe('FinishedScreen', () => {
  afterEach(() => {
    cleanup();
    provisionShell.mockClear();
    closeWizard.mockClear();
  });

  it('показывает итог установки: филиал, роль и имя ПК', () => {
    renderFinished('gaming_pc', { status: 'installed', exitCode: 0, message: null });

    expect(screen.getByText('Главный зал')).toBeInTheDocument();
    expect(screen.getByText('AFK4-PC-12')).toBeInTheDocument();
  });

  // Зелёная плашка «всё хорошо» только шумит: строка появляется, когда есть что чинить.
  it('удачную установку отдельной строкой не празднует', () => {
    renderFinished('gaming_pc', { status: 'installed', exitCode: 0, message: null });

    expect(screen.queryByRole('button', { name: 'Повторить установку' })).toBeNull();
  });

  // На рабочем месте управляющего ставится панель, а не оболочка игрока: одна строка на обе роли
  // обещала управляющему то, чего у него не будет.
  it('называет то приложение, которое ставится этой роли', () => {
    renderFinished('manager_workstation');

    expect(screen.getByText('Не удалось установить Organization Admin.')).toBeInTheDocument();
  });

  it('на игровом ПК называет оболочку игрока', () => {
    renderFinished('gaming_pc');

    expect(screen.getByText('Не удалось установить Оболочка игрока.')).toBeInTheDocument();
  });

  it('удачный повтор убирает строку с ошибкой', async () => {
    renderFinished('gaming_pc');

    fireEvent.click(screen.getByRole('button', { name: 'Повторить установку' }));

    await waitFor(() => expect(screen.queryByRole('button', { name: 'Повторить установку' })).toBeNull());
    expect(provisionShell.mock.calls[0]![0]).toBe('gaming_pc');
  });

  // Ради этого тест и написан: без catch кнопка молча возвращалась в исходное состояние, и
  // человек у ПК видел ровно то же, что до нажатия, — будто она не работает.
  it('сорвавшийся повтор называет причину, а не молчит', async () => {
    provisionShell.mockImplementationOnce(async () => {
      throw new HostBridgeRequestError('msiexec 1603', 'wizard_shell_provision_failed', null);
    });
    renderFinished('gaming_pc');

    fireEvent.click(screen.getByRole('button', { name: 'Повторить установку' }));

    expect(await screen.findByText('Повтор не удался.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Повторить установку' })).toBeEnabled();
  });

  // Место без связи с хостом — не «повтор не удался», а «мастер не достучался до агента».
  it('обрыв моста называет своими словами', async () => {
    provisionShell.mockImplementationOnce(async () => {
      throw new HostBridgeUnavailableError();
    });
    renderFinished('gaming_pc');

    fireEvent.click(screen.getByRole('button', { name: 'Повторить установку' }));

    expect(await screen.findByText(/Не удаётся связаться с локальным агентом/)).toBeInTheDocument();
  });

  it('кнопка закрытия закрывает мастер', () => {
    renderFinished('gaming_pc', { status: 'installed', exitCode: 0, message: null });

    fireEvent.click(screen.getByRole('button', { name: 'Завершить' }));

    expect(closeWizard).toHaveBeenCalledTimes(1);
  });
});
