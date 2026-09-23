import { describe, it, expect, mock, beforeEach } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { WizardBranch, WizardEnrollResult, WizardSeat } from './wizardApi';
import { DeviceScreen } from './DeviceScreen';
import { HostBridgeRequestError, hostBridgeTimeoutCode } from './hostBridge';

const ZONE = { zoneId: 'z-1', name: 'Main Hall', sortOrder: 0 };
const BRANCH: WizardBranch = {
  branchId: 'b-1',
  branchSlug: 'main',
  branchName: 'Главный',
  zones: [ZONE],
  seats: [],
  freeSeatIds: [],
  hasTariff: false,
  hasStaffBesidesOwner: false,
};
const CREATED_SEAT: WizardSeat = {
  seatId: 's-1',
  pcName: 'PC-NEW',
  zoneId: 'z-1',
  zoneName: 'Main Hall',
  sortOrder: 1,
  status: 'Free',
  deviceId: null,
  deviceName: null,
  isOnline: null,
};
const ENROLL_RESULT = { deviceId: 'd-1', displayName: 'PC-NEW' } as unknown as WizardEnrollResult;

// No mock.module('./wizardApi') here — DeviceScreen receives installClient as a prop and never
// imports wizardApi functions directly, so install ops are mocked via the prop instead.
// (Sibling tests use mock.module because their subjects import wizardApi at the module level;
// avoid adding mock.module here as bun's cross-file mock.module pollution can bleed into those.)
const createSeat = mock(async () => CREATED_SEAT);
const enrollDevice = mock(async () => ENROLL_RESULT);

function renderScreen(props: Partial<Parameters<typeof DeviceScreen>[0]> = {}) {
  const onEnrolled = mock(() => {});
  const onBack = mock(() => {});
  render(
    <I18nProvider>
      <DeviceScreen
        stepNumber={1}
        installClient={{ createSeat, enrollDevice }}
        ownerName="Сотрудник"
        branch={BRANCH}
        role="gaming_pc"
        defaultDisplayName="PREVIEW-PC"
        onEnrolled={onEnrolled}
        onBack={onBack}
        {...props}
      />
    </I18nProvider>,
  );
  return { onEnrolled, onBack };
}

const submit = () => fireEvent.click(screen.getByRole('button', { name: /зарегистрировать/i }));

describe('DeviceScreen (create-only)', () => {
  beforeEach(() => {
    createSeat.mockClear();
    enrollDevice.mockClear();
    createSeat.mockImplementation(async () => CREATED_SEAT);
    enrollDevice.mockImplementation(async () => ENROLL_RESULT);
  });

  it('gaming_pc: creates a seat named after the PC, then enrolls into it', async () => {
    const { onEnrolled } = renderScreen();
    submit();
    await waitFor(() => expect(enrollDevice).toHaveBeenCalledTimes(1));
    expect(createSeat).toHaveBeenCalledWith({
      branchId: 'b-1',
      zoneId: 'z-1',
      zoneName: 'Main Hall',
      name: 'PREVIEW-PC',
    });
    expect(enrollDevice).toHaveBeenCalledWith({
      branchId: 'b-1',
      seatId: 's-1',
      role: 'gaming_pc',
      displayName: 'PREVIEW-PC',
    });
    expect(onEnrolled).toHaveBeenCalledWith(ENROLL_RESULT, CREATED_SEAT);
  });

  it('manager_workstation: enrolls without a seat and never creates one', async () => {
    const { onEnrolled } = renderScreen({ role: 'manager_workstation' });
    submit();
    await waitFor(() => expect(enrollDevice).toHaveBeenCalledTimes(1));
    expect(createSeat).not.toHaveBeenCalled();
    expect(enrollDevice).toHaveBeenCalledWith({
      branchId: 'b-1',
      seatId: null,
      role: 'manager_workstation',
      displayName: 'PREVIEW-PC',
    });
    expect(onEnrolled).toHaveBeenCalledWith(ENROLL_RESULT, null);
  });

  it('blocks enroll when the name is too short', async () => {
    renderScreen({ defaultDisplayName: 'PC' });
    expect((screen.getByRole('button', { name: /зарегистрировать/i }) as HTMLButtonElement).disabled).toBe(true);
    submit();
    expect(createSeat).not.toHaveBeenCalled();
    expect(enrollDevice).not.toHaveBeenCalled();
  });

  // Отказ объясняется по коду моста, а не пересказом исключения. Прошлая версия этого теста
  // бросала Error с русским текстом и проверяла, что он появился на экране, — то есть закрепляла
  // как правильное ровно то, из-за чего в русский мастер приезжали «Seat name is required.» и
  // «Platform API returned 500 for /api/…».
  it('объясняет неудачу создания места и не регистрирует ПК', async () => {
    createSeat.mockImplementation(async () => {
      throw new HostBridgeRequestError('Platform API returned 500 for /api/…', 'wizard_create_seat_failed', null);
    });
    renderScreen();
    submit();
    await waitFor(() => expect(screen.getByRole('alert').textContent)
      .toContain('Не удалось создать место'));
    expect(screen.getByRole('alert').textContent).not.toContain('Platform API');
    expect(enrollDevice).not.toHaveBeenCalled();
  });

  // Незнакомый код — общая надпись, а не пересказ английской строки: выдумывать объяснение коду,
  // которого мы не знаем, хуже, чем сказать «не получилось».
  it('незнакомую ошибку называет общими словами, не показывая её текст', async () => {
    createSeat.mockImplementation(async () => {
      throw new Error('No such host is known.');
    });
    renderScreen();
    submit();
    await waitFor(() => expect(screen.getByRole('alert').textContent)
      .toContain('Не удалось выполнить действие'));
    expect(screen.getByRole('alert').textContent).not.toContain('No such host');
  });

  it('молчание моста называет молчанием моста', async () => {
    createSeat.mockImplementation(async () => {
      throw new HostBridgeRequestError('Native host bridge request timed out.', hostBridgeTimeoutCode, null);
    });
    renderScreen();
    submit();
    await waitFor(() => expect(screen.getByRole('alert').textContent)
      .toContain('не ответил вовремя'));
  });
});

// Зал заводится на рабочем месте управляющего, а потом каждый игровой ПК проходит этот экран.
// Пока он всегда создавал новое место, собственный счастливый путь мастера плодил дубли: на
// десять машин в зале получалось двадцать мест.
describe('выбор уже заведённого места', () => {
  const EXISTING: WizardSeat = {
    seatId: 's-7', pcName: 'ПК-7', zoneId: 'z-1', zoneName: 'Main Hall', sortOrder: 7,
    status: 'Free', deviceId: null, deviceName: null, isOnline: null,
  };
  const OTHER: WizardSeat = { ...EXISTING, seatId: 's-2', pcName: 'ПК-2', sortOrder: 2 };
  const BRANCH_WITH_SEATS: WizardBranch = {
    ...BRANCH,
    seats: [OTHER, EXISTING],
    freeSeatIds: ['s-2', 's-7'],
  };

  beforeEach(() => {
    createSeat.mockClear();
    enrollDevice.mockClear();
  });

  it('привязывает ПК к выбранному месту и НЕ создаёт новое', async () => {
    const { onEnrolled } = renderScreen({ branch: BRANCH_WITH_SEATS, defaultDisplayName: 'ПК-7' });

    submit();

    await waitFor(() => expect(enrollDevice).toHaveBeenCalled());
    expect(createSeat).not.toHaveBeenCalled();
    expect(enrollDevice).toHaveBeenCalledWith(expect.objectContaining({ seatId: 's-7' }));
    expect(onEnrolled).toHaveBeenCalled();
  });

  // Умолчание — место с именем этой машины: человек подтверждает догадку, а не ищет себя в списке.
  it('по умолчанию предлагает место с именем машины', () => {
    renderScreen({ branch: BRANCH_WITH_SEATS, defaultDisplayName: 'ПК-7' });

    expect((screen.getByLabelText(/какое это место/i) as HTMLSelectElement).value).toBe('s-7');
  });

  it('позволяет всё-таки завести новое место', async () => {
    renderScreen({ branch: BRANCH_WITH_SEATS, defaultDisplayName: 'ПК-7' });

    fireEvent.change(screen.getByLabelText(/какое это место/i), { target: { value: '__new__' } });
    submit();

    await waitFor(() => expect(createSeat).toHaveBeenCalled());
  });

  // Пустой зал: спрашивать не о чем, экран остаётся прежним.
  it('не спрашивает про место, когда свободных нет', () => {
    renderScreen();

    expect(screen.queryByLabelText(/какое это место/i)).toBeNull();
  });

  // Место заведётся само — сказать об этом надо до нажатия, иначе в зале молча появляется ещё
  // одна строка, о которой человек узнаёт уже из Панели AFK4.net.
  it('свободных мест нет — экран говорит, что заведёт новое', () => {
    renderScreen();

    expect(screen.getByText(/заведём новое/i)).toBeInTheDocument();
  });

  // Зала нет вовсе — место создавать негде. Раньше кнопка просто не нажималась, и причину
  // человек у ПК не узнавал ниоткуда.
  it('зала в филиале нет — экран объясняет, почему регистрация недоступна', () => {
    renderScreen({ branch: { ...BRANCH, zones: [] } });

    expect(screen.getByRole('alert')).toHaveTextContent(/ещё нет ни одного зала/i);
    expect(screen.getByRole('button', { name: /зарегистрировать/i })).toBeDisabled();
  });
});
