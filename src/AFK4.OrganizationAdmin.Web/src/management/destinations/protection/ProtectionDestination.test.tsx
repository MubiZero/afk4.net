import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../../operatorToast';
import { PlatformApiError } from '../../../platformApi';
import type { BranchProtectionProfileDto } from '../../../api/clients/settings';

const organizationId = 'o1';

function profile(overrides: Partial<BranchProtectionProfileDto['profile']> = {}, updatedAtUtc: string | null = null): BranchProtectionProfileDto {
  return {
    organizationId,
    branchId: 'b1',
    profile: {
      version: 0,
      blockRemovableStorage: false,
      blockBrowserDownloads: false,
      blockBrowserIncognito: false,
      disableRunDialog: false,
      hiddenDrives: [],
      urlBlocklist: [],
      blockedWindows: [],
      clearAfterSession: ['steam', 'browsers', 'launchers', 'messengers'],
      idleShutdownMinutes: null,
      clubRules: null,
      ...overrides
    },
    updatedAtUtc
  };
}

let stored = profile();
const getProtectionProfile = mock(async (): Promise<BranchProtectionProfileDto> => stored);
const updateProtectionProfile = mock(async (_branchId: string, request: Record<string, unknown>): Promise<BranchProtectionProfileDto> => {
  const { organizationId: _o, expectedVersion, ...rest } = request as Record<string, unknown> & { expectedVersion: number };
  stored = profile({ ...(rest as object), version: expectedVersion + 1 }, '2026-09-25T09:00:00Z');
  return stored;
});

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({ settings: { getProtectionProfile, updateProtectionProfile } })
}));

const { ProtectionDestination } = await import('./ProtectionDestination');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId }, branchId: 'b1' } as never;

function renderScreen() {
  return render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <ProtectionDestination backend={backend} session={{ permissions: [], organizationId } as never} currencyCode="TJS" />
      </ToastProvider>
    </I18nProvider>
  );
}

afterEach(() => {
  stored = profile();
  getProtectionProfile.mockClear();
  updateProtectionProfile.mockClear();
  cleanup();
});

afterAll(() => mock.module('../../../operatorHelpers', () => actual));

describe('ProtectionDestination', () => {
  it('говорит, что защиту не настраивали, и не делает вид, будто что-то запрещено', async () => {
    renderScreen();

    expect(await screen.findByLabelText('Запретить флешки и внешние диски')).not.toBeChecked();
    expect(screen.getByText(/Защиту ещё не настраивали/)).toBeInTheDocument();
  });

  // «Скрыть диск» прячет его в Проводнике, но не запрещает — клуб должен знать это заранее.
  it('честно подписывает скрытие дисков: это не запрет', async () => {
    renderScreen();

    await screen.findByLabelText('Запретить флешки и внешние диски');
    expect(screen.getByText(/Это не запрет: программа всё равно откроет диск по пути/)).toBeInTheDocument();
  });

  it('сохраняет правила одним PUT с версией, которую открыл человек', async () => {
    stored = profile({ version: 2, urlBlocklist: ['old.example'] }, '2026-09-20T12:00:00Z');
    renderScreen();

    fireEvent.click(await screen.findByLabelText('Запретить флешки и внешние диски'));
    fireEvent.click(screen.getByRole('button', { name: 'Диск D' }));
    fireEvent.change(screen.getByLabelText('Заблокированные сайты'), { target: { value: 'old.example\n *.casino.example \n' } });
    fireEvent.change(screen.getByLabelText('Закрывать окна с заголовком'), { target: { value: 'Командная строка' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateProtectionProfile).toHaveBeenCalledTimes(1));
    expect(updateProtectionProfile.mock.calls[0][1]).toEqual({
      organizationId,
      expectedVersion: 2,
      blockRemovableStorage: true,
      blockBrowserDownloads: false,
      blockBrowserIncognito: false,
      disableRunDialog: false,
      hiddenDrives: ['D'],
      urlBlocklist: ['old.example', '*.casino.example'],
      blockedWindows: [{ titleContains: 'Командная строка', className: null }],
      clearAfterSession: ['steam', 'browsers', 'launchers', 'messengers'],
      idleShutdownMinutes: null,
      clubRules: null
    });
    expect(await screen.findByRole('button', { name: 'Диск D' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('выключение при простое и правила клуба уходят в профиль', async () => {
    renderScreen();

    fireEvent.change(await screen.findByLabelText('Выключать свободный ПК'), { target: { value: '60' } });
    fireEvent.change(screen.getByLabelText('Правила клуба'), { target: { value: 'Наушники — у администратора.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateProtectionProfile).toHaveBeenCalledTimes(1));
    expect(updateProtectionProfile.mock.calls[0][1]).toMatchObject({ idleShutdownMinutes: 60, clubRules: 'Наушники — у администратора.' });
  });

  // По умолчанию после сессии стирается всё; клуб выключает пункт — он уходит из профиля.
  it('стирает следы по умолчанию и сохраняет выключенный пункт', async () => {
    renderScreen();

    expect(await screen.findByLabelText('Вход в Steam')).toBeChecked();
    expect(screen.getByLabelText('Мессенджеры')).toBeChecked();
    expect(screen.getByText(/Сохранения игр не трогаются/)).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('Мессенджеры'));
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateProtectionProfile).toHaveBeenCalledTimes(1));
    expect(updateProtectionProfile.mock.calls[0][1].clearAfterSession).toEqual(['steam', 'browsers', 'launchers']);
  });

  it('чужая правка поверх открытой — просит обновить, а не затирает', async () => {
    updateProtectionProfile.mockImplementationOnce(async () => {
      throw new PlatformApiError('Platform API returned 409', 409, 'Conflict', JSON.stringify({ error: 'The protection profile was saved by someone else.', code: 'protection_profile_version_conflict' }));
    });
    renderScreen();

    fireEvent.click(await screen.findByLabelText('Отключить «Выполнить» (Win+R)'));
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText(/Защиту уже изменил кто-то другой/)).toBeInTheDocument();
  });
});
