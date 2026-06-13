import { describe, it, expect, mock, beforeEach } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { WizardBranch, WizardEnrollResult, WizardSeat } from './wizardApi';
import { DeviceScreen } from './DeviceScreen';

const ZONE = { zoneId: 'z-1', name: 'Main Hall', sortOrder: 0 };
const BRANCH: WizardBranch = {
  branchId: 'b-1',
  branchSlug: 'main',
  branchName: 'Главный',
  zones: [ZONE],
  seats: [],
  freeSeatIds: [],
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

  it('shows an error and skips enroll when seat creation fails', async () => {
    createSeat.mockImplementation(async () => {
      throw new Error('Сервер недоступен');
    });
    renderScreen();
    submit();
    await waitFor(() => expect(screen.getByRole('alert').textContent).toContain('Сервер недоступен'));
    expect(enrollDevice).not.toHaveBeenCalled();
  });
});
