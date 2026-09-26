import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { InstallCodeDto } from '../../api/clients/installCodes';
import { InstallCodesSection } from './InstallCodesSection';

afterEach(() => cleanup());

const branches = [
  { branchId: 'b1', name: 'Центр' },
  { branchId: 'b2', name: 'Сино' }
];

function code(overrides: Partial<InstallCodeDto> = {}): InstallCodeDto {
  return {
    installCodeId: 'c1',
    branchId: 'b2',
    code: null,
    createdAtUtc: '2026-09-25T09:00:00Z',
    expiresAtUtc: '2026-09-26T09:00:00Z',
    maxDevices: 30,
    usedDevices: 3,
    ...overrides
  };
}

function clients(installCodes: Record<string, unknown>) {
  return { installCodes } as never;
}

function renderSection(installCodes: Record<string, unknown>, preferredBranchId: string | null = 'b2') {
  return render(
    <I18nProvider initialLocale="ru">
      <InstallCodesSection
        clients={clients(installCodes)}
        branches={branches}
        preferredBranchId={preferredBranchId}
        installerUrl="https://dl.afk4.net/afk4-client-1.4.0-stable.exe"
      />
    </I18nProvider>
  );
}

describe('InstallCodesSection', () => {
  it('opens on the branch the Panel is in and lists its live codes', async () => {
    const list = mock(async () => [code()]);
    renderSection({ list, issue: mock(), revoke: mock() });

    expect(await screen.findByText(/поставлено 3 из 30/)).toBeInTheDocument();
    expect(list).toHaveBeenCalledWith('b2');
  });

  // Код виден один раз — сразу после выдачи, вместе с готовой командой для скрипта.
  it('shows the issued code once, with the ready command', async () => {
    const issued = code({ installCodeId: 'c2', code: '7KQ2-M9XD-4TPV-HB3R', usedDevices: 0 });
    const issue = mock(async () => issued);
    renderSection({ list: mock(async () => []), issue, revoke: mock() });
    await screen.findByText('Действующих кодов нет.');

    fireEvent.change(screen.getByLabelText('Сколько ПК'), { target: { value: '12' } });
    fireEvent.click(screen.getByRole('button', { name: 'Выдать код' }));

    expect(await screen.findByText('7KQ2-M9XD-4TPV-HB3R')).toBeInTheDocument();
    expect(screen.getByText('afk4-client-1.4.0-stable.exe /quiet AFK4_INSTALL_CODE=7KQ2-M9XD-4TPV-HB3R')).toBeInTheDocument();
    expect(issue).toHaveBeenCalledWith('b2', { lifetimeHours: 24, maxDevices: 12 });
  });

  it('does not issue a code for an impossible number of PCs', async () => {
    const issue = mock();
    renderSection({ list: mock(async () => []), issue, revoke: mock() });
    await screen.findByText('Действующих кодов нет.');

    fireEvent.change(screen.getByLabelText('Сколько ПК'), { target: { value: '500' } });

    expect(screen.getByRole('button', { name: 'Выдать код' })).toBeDisabled();
    expect(screen.getByText('Число ПК — от 1 до 200.')).toBeInTheDocument();
    expect(issue).not.toHaveBeenCalled();
  });

  it('revokes a code and reloads the list', async () => {
    let live = [code()];
    const list = mock(async () => live);
    const revoke = mock(async () => { live = []; });
    renderSection({ list, issue: mock(), revoke });
    await screen.findByText(/поставлено 3 из 30/);

    fireEvent.click(screen.getByRole('button', { name: 'Отозвать' }));

    await waitFor(() => expect(screen.getByText('Действующих кодов нет.')).toBeInTheDocument());
    expect(revoke).toHaveBeenCalledWith('b2', 'c1');
  });

  // Сервер отказал — причина видна тут же, а не теряется в общем «не удалось».
  it('names the reason when the server refuses', async () => {
    const issue = mock(async () => { throw new Error('Нет права ставить ПК в этом филиале.'); });
    renderSection({ list: mock(async () => []), issue, revoke: mock() });
    await screen.findByText('Действующих кодов нет.');

    fireEvent.click(screen.getByRole('button', { name: 'Выдать код' }));

    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });

  it('switching the branch hides the code issued for another one', async () => {
    const issued = code({ installCodeId: 'c2', code: '7KQ2-M9XD-4TPV-HB3R' });
    renderSection({ list: mock(async () => []), issue: mock(async () => issued), revoke: mock() });
    await screen.findByText('Действующих кодов нет.');
    fireEvent.click(screen.getByRole('button', { name: 'Выдать код' }));
    await screen.findByText('7KQ2-M9XD-4TPV-HB3R');

    fireEvent.change(screen.getByLabelText('Филиал'), { target: { value: 'b1' } });

    await waitFor(() => expect(screen.queryByText('7KQ2-M9XD-4TPV-HB3R')).toBeNull());
  });
});
