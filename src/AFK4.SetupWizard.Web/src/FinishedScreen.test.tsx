import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { FinishedScreen } from './FinishedScreen';
import { HostBridgeRequestError, HostBridgeUnavailableError } from './hostBridge';
import type { WizardEnrollResult, WizardRole, WizardSeat, WizardShellOutcome } from './wizardApi';

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
  options: { pending?: boolean; seat?: WizardSeat | null } = {},
) {
  const onClose = mock(() => {});
  const result = { ...enrolled(role, shell), ...(options.pending ? { enrollmentState: 'pending' } : {}) };
  render(
    <I18nProvider initialLocale="ru">
      <FinishedScreen
        result={result}
        branchName="Главный зал"
        selectedSeat={options.seat ?? null}
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

    expect(screen.getByText(/Перезагрузите ПК — Windows сама войдёт в учётку игрока/)).toBeInTheDocument();
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
  // «Откройте панель клуба» — два имени одного места на одном экране. Теперь оба — Панель AFK4.NET.
  it('называет то приложение, которое ставится этой роли', () => {
    renderFinished('manager_workstation');

    expect(screen.getByText('Не удалось установить Панель AFK4.NET.')).toBeInTheDocument();
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

  // Главная на экране одна. Раньше «Перезагрузить сейчас» и «Завершить» были обе залиты: какая
  // из них следующий шаг, экран не говорил.
  it('с киоском главная — перезагрузка, а закрыть можно тихой ссылкой', () => {
    const { onClose, reboot } = renderFinished('gaming_pc', { ...installed, kiosk: { status: 'ready', message: null } });

    const primaries = screen.getAllByRole('button').filter((button) => button.classList.contains('ui-btn--primary'));
    expect(primaries.map((button) => button.textContent)).toEqual(['Перезагрузить сейчас']);
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть без перезагрузки' }));
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(reboot).not.toHaveBeenCalled();
  });

  // Две плашки спорили: зелёная звала перезагрузить сейчас, жёлтая — сначала подтвердить ПК в
  // Панели. Теперь это один список по порядку.
  it('ждущий подтверждения ПК: сначала подтвердить, потом перезагрузить', () => {
    renderFinished('gaming_pc', { ...installed, kiosk: { status: 'ready', message: null } }, undefined, undefined, { pending: true });

    const steps = screen.getAllByRole('listitem').map((item) => item.textContent ?? '');
    expect(steps).toHaveLength(2);
    expect(steps[0]).toMatch(/^Подтвердите этот ПК в Панели AFK4.NET/);
    expect(steps[1]).toMatch(/^Перезагрузите ПК/);
  });

  // По одному залу ПК не найти: итог называет место так же, как карта зала.
  it('итог игрового ПК называет место — зал и имя места', () => {
    const seat: WizardSeat = {
      seatId: 'seat-5', pcName: 'ПК-5', zoneId: 'zone-1', zoneName: 'Общий зал', sortOrder: 5,
      status: 'free', deviceId: null, deviceName: null, isOnline: null,
    };
    renderFinished('gaming_pc', installed, undefined, undefined, { seat });

    expect(screen.getByText('Общий зал · ПК-5')).toBeInTheDocument();
  });

  // Ради этого тест и написан: служба запущена, а агент настройку не принял — и экран говорил
  // «ПК подключён, принимает команды» про машину, которая молчит.
  it('агент не принял настройку — не говорит «ПК подключён», а называет что не так', () => {
    renderFinished('gaming_pc', { status: 'agent_not_ready', exitCode: 0, message: 'the agent idles' });

    expect(screen.getByRole('heading', { name: 'ПК записан, но пока не на связи' })).toBeInTheDocument();
    expect(screen.queryByText('ПК подключён')).toBeNull();
    expect(screen.queryByText('ПК уже принимает команды от сервера.')).toBeNull();
    expect(screen.getByText(/служба AFK4 не приняла настройку/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Повторить установку' })).toBeInTheDocument();
  });

  it('служба не запустилась — тоже не «подключён»', () => {
    renderFinished('gaming_pc', agentStartFailed);

    expect(screen.getByRole('heading', { name: 'ПК записан, но пока не на связи' })).toBeInTheDocument();
  });

  it('приложение не встало — агент не запускали, «подключён» тоже не говорим', () => {
    renderFinished('gaming_pc', failedShell);

    expect(screen.queryByText('ПК подключён')).toBeNull();
  });

  // Удачный повтор должен сменить и заголовок, а не только убрать строку с ошибкой.
  it('удачный повтор возвращает «ПК подключён»', async () => {
    renderFinished('gaming_pc', { status: 'agent_not_ready', exitCode: 0, message: null });

    fireEvent.click(screen.getByRole('button', { name: 'Повторить установку' }));

    expect(await screen.findByRole('heading', { name: 'ПК подключён' })).toBeInTheDocument();
  });

  it('агент принял настройку — «ПК подключён»', () => {
    renderFinished('gaming_pc', installed);

    expect(screen.getByRole('heading', { name: 'ПК подключён' })).toBeInTheDocument();
  });
});
