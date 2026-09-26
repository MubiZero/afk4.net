import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { DeviceHardwareDto, HardwareSnapshotDto } from '@afk4/contracts';
import { DeviceHardwareSection } from './DeviceHardwareSection';

afterEach(cleanup);

const snapshot = (gpu: string): HardwareSnapshotDto => ({
  cpu: 'AMD Ryzen 5 5600X', cpuThreads: 12, memoryGb: 16, gpus: [{ name: gpu, memoryGb: 12 }],
  motherboard: 'ASUS PRIME B550M-A', disks: [{ name: 'C:', sizeGb: 500 }], os: 'Windows 11 Pro 23H2', bios: null
});

function changed(): DeviceHardwareDto {
  return {
    current: snapshot('RTX 4060'), reportedAtUtc: '2026-09-25T10:00:00Z',
    accepted: snapshot('RTX 3060'), acceptedAtUtc: '2026-09-20T10:00:00Z', acceptedByName: null,
    changes: [{ component: 'gpu', was: 'RTX 3060 12 GB', now: 'RTX 4060 12 GB' }]
  };
}

function renderSection(devices: Record<string, unknown>, canAccept = true, onAccepted = () => {}) {
  return render(
    <I18nProvider initialLocale="ru">
      <DeviceHardwareSection clients={{ devices } as never} deviceId="d1" canAccept={canAccept} onAccepted={onAccepted} />
    </I18nProvider>
  );
}

describe('DeviceHardwareSection', () => {
  it('shows what changed — was and now — and accepts it as normal', async () => {
    const accepted = { ...changed(), accepted: snapshot('RTX 4060'), changes: [], acceptedByName: 'Шерзод', acceptedAtUtc: '2026-09-25T11:00:00Z' };
    const acceptHardware = mock(async () => accepted);
    const onAccepted = mock(() => {});
    renderSection({ getHardware: mock(async () => changed()), acceptHardware }, true, onAccepted);

    expect(await screen.findByText('Железо изменилось')).toBeInTheDocument();
    expect(screen.getByText('RTX 3060 12 GB')).toBeInTheDocument();
    expect(screen.getByText('RTX 4060 12 GB')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Принять как норму' }));

    await waitFor(() => expect(onAccepted).toHaveBeenCalledTimes(1));
    expect(screen.queryByText('Железо изменилось')).toBeNull();
    expect(screen.getByText(/норму принял Шерзод/)).toBeInTheDocument();
  });

  it('without the right, the change is visible but cannot be accepted', async () => {
    renderSection({ getHardware: mock(async () => changed()), acceptHardware: mock() }, false);

    expect(await screen.findByText('Железо изменилось')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Принять как норму' })).toBeNull();
  });

  it('lists drives and monitors, and names a missing monitor in the changes', async () => {
    const current: HardwareSnapshotDto = {
      ...snapshot('RTX 4060'),
      physicalDisks: [{ model: 'Samsung SSD 980 PRO 1TB', sizeGb: 1000, interface: 'NVMe' }, { model: 'WDC WD10EZEX', sizeGb: 1000, interface: null }],
      monitors: [{ name: 'S24R35x', manufacturer: 'SAM', serial: 'H4ZN500123' }]
    };
    const hardware: DeviceHardwareDto = {
      ...changed(), current,
      changes: [
        { component: 'monitor', was: 'DELL P2419H (CFV9N93), S24R35x (H4ZN500123)', now: 'S24R35x (H4ZN500123)' },
        { component: 'physical_disk', was: 'Kingston SA400S37240G 240 GB', now: null }
      ]
    };
    renderSection({ getHardware: mock(async () => hardware), acceptHardware: mock() });

    expect(await screen.findByText('Samsung SSD 980 PRO 1TB · 1000 GB · NVMe, WDC WD10EZEX · 1000 GB')).toBeInTheDocument();
    expect(screen.getByText('S24R35x (H4ZN500123)', { selector: '.settings-device-detail-grid b' })).toBeInTheDocument();
    const changes = screen.getByRole('status');
    expect(within(changes).getByText('Мониторы')).toBeInTheDocument();
    expect(within(changes).getByText('DELL P2419H (CFV9N93), S24R35x (H4ZN500123)')).toBeInTheDocument();
    expect(within(changes).getByText('Накопители')).toBeInTheDocument();
    expect(within(changes).getByText('нет')).toBeInTheDocument();
  });

  // Старый агент накопителей и мониторов не присылает — это «неизвестно», а не «нет».
  it('hides drives and monitors the PC did not report, but shows an empty list as a dash', async () => {
    const hardware: DeviceHardwareDto = { ...changed(), changes: [], current: { ...snapshot('RTX 4060'), monitors: [] } };
    renderSection({ getHardware: mock(async () => hardware), acceptHardware: mock() });

    expect(await screen.findByText('Мониторы')).toBeInTheDocument();
    expect(screen.queryByText('Накопители')).toBeNull();
  });

  it('a PC that never reported says when the snapshot comes', async () => {
    renderSection({ getHardware: mock(async () => ({ current: null, reportedAtUtc: null, accepted: null, acceptedAtUtc: null, acceptedByName: null, changes: [] })), acceptHardware: mock() });

    expect(await screen.findByText(/раз в сутки/)).toBeInTheDocument();
  });
});
